using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Application.Pupils.Records;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;
using SkiaSharp;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Spec 6.5.4, 6.5.8 and 9.6: pupil photographs and document-checklist scans through the real pipeline (CSRF, required
/// Idempotency-Key, the processor, the in-memory store), against a PENDING admission, so every privilege is checked in the
/// handler. Signs in as a seeded Super Admin.
/// </summary>
public sealed class PupilFileEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    private static int _nextYear = 7700;

    [Fact]
    public async Task Photo_UploadServesBothSizes_ClearsTheChasedItem_ThenRemovalIsAudited()
    {
        RequireDatabase();
        var pupilId = await SeedPendingAdmissionAsync();
        var jar = await SignInAsync();
        var before = await ReadAsync<AdmissionCompletenessDto>(await GetAsync($"/api/v1/admissions/{pupilId}/completeness", jar));
        before.Chased.ShouldContain(item => item.Code == "pupil.photograph");

        var upload = await UploadAsync($"/api/v1/pupils/{pupilId}/photo", jar, Image(900, 600, SKEncodedImageFormat.Png));

        upload.StatusCode.ShouldBe(HttpStatusCode.OK);
        var photo = await ReadAsync<PupilPhotoDto>(upload);
        photo.PhotoUrl.ShouldBe($"/api/v1/pupils/{pupilId}/photo");
        photo.ThumbnailUrl.ShouldBe($"/api/v1/pupils/{pupilId}/photo/thumbnail");

        var standard = await GetAsync(photo.PhotoUrl, jar);
        standard.StatusCode.ShouldBe(HttpStatusCode.OK);
        standard.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
        standard.Content.Headers.ContentDisposition!.ToString().ShouldBe("inline; filename=photo.jpg");
        standard.Headers.CacheControl!.Private.ShouldBeTrue();
        standard.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        using (var decoded = SKBitmap.Decode(await standard.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)))
        {
            (decoded.Width, decoded.Height).ShouldBe((400, 400));
        }

        using (var thumbnail = SKBitmap.Decode(await (await GetAsync(photo.ThumbnailUrl, jar)).Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)))
        {
            (thumbnail.Width, thumbnail.Height).ShouldBe((96, 96));
        }

        var pupil = await ReadAsync<PupilDto>(await GetAsync($"/api/v1/pupils/{pupilId}", jar));
        pupil.PhotoUpdatedAtUtc.ShouldBe(photo.UpdatedAtUtc);
        var after = await ReadAsync<AdmissionCompletenessDto>(await GetAsync($"/api/v1/admissions/{pupilId}/completeness", jar));
        after.Chased.ShouldNotContain(item => item.Code == "pupil.photograph");

        (await DeleteAsync($"/api/v1/pupils/{pupilId}/photo", jar)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync(photo.PhotoUrl, jar)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await DeleteAsync($"/api/v1/pupils/{pupilId}/photo", jar)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var actions = await AuditActionsAsync(pupilId);
        actions.ShouldContain("pupil.photo.update");
        actions.ShouldContain("pupil.photo.remove");
    }

    [Fact]
    public async Task Photo_OverThreeMegabytes_Or_Svg_Is422_WithTheSpecMessage()
    {
        RequireDatabase();
        var pupilId = await SeedPendingAdmissionAsync();
        var jar = await SignInAsync();

        var tooLarge = await ReadRootAsync(await UploadAsync($"/api/v1/pupils/{pupilId}/photo", jar, new byte[(3 * 1024 * 1024) + 1]));
        tooLarge.GetProperty("errorCode").GetString().ShouldBe("pupil_photo.too_large");
        tooLarge.GetProperty("detail").GetString()!.ShouldStartWith("This photograph is 3.1 MB. The limit is 3 MB.");

        var svg = await UploadAsync($"/api/v1/pupils/{pupilId}/photo", jar, "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray());
        svg.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ReadRootAsync(svg)).GetProperty("errorCode").GetString().ShouldBe("pupil_photo.unsupported_type");
    }

    [Fact]
    public async Task DocumentScan_TicksTheRow_DownloadsAsAnAttachment_AndRemovalKeepsTheTick()
    {
        RequireDatabase();
        var pupilId = await SeedPendingAdmissionAsync();
        var jar = await SignInAsync();
        var pdf = "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n"u8.ToArray();
        var fileUrl = $"/api/v1/pupils/{pupilId}/documents/BirthCertificate/file";

        var upload = await UploadAsync(fileUrl, jar, pdf);

        upload.StatusCode.ShouldBe(HttpStatusCode.OK);
        var row = (await ReadAsync<PupilDocumentListDto>(upload)).Items.Single(item => item.DocumentType == PupilDocumentType.BirthCertificate);
        row.Received.ShouldBeTrue();
        row.ReceivedDate.ShouldBe(Application.Weekly.WeeklyProjection.LagosToday(DateTimeOffset.UtcNow));
        row.File.ShouldNotBeNull().ContentType.ShouldBe("application/pdf");
        row.File.SizeBytes.ShouldBe(pdf.Length);

        var download = await GetAsync(fileUrl, jar);
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        download.Content.Headers.ContentDisposition!.ToString().ShouldBe("attachment; filename=birth-certificate.pdf");
        (await download.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBe(pdf);

        var removed = await DeleteAsync(fileUrl, jar);
        removed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterRemoval = (await ReadAsync<PupilDocumentListDto>(removed)).Items.Single(item => item.DocumentType == PupilDocumentType.BirthCertificate);
        afterRemoval.File.ShouldBeNull();
        afterRemoval.Received.ShouldBeTrue();
        (await GetAsync(fileUrl, jar)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var actions = await AuditActionsAsync(pupilId);
        actions.ShouldContain("pupil.document.file_attached");
        actions.ShouldContain("pupil.document.file_removed");
    }

    [Fact]
    public async Task DocumentScan_OnTheUnlabelledOtherRow_Or_AsAGif_Is422()
    {
        RequireDatabase();
        var pupilId = await SeedPendingAdmissionAsync();
        var jar = await SignInAsync();

        var other = await UploadAsync($"/api/v1/pupils/{pupilId}/documents/Other/file", jar, Image(40, 40, SKEncodedImageFormat.Jpeg));
        other.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ReadRootAsync(other)).GetProperty("errorCode").GetString().ShouldBe("document.other_label_required");

        var gif = await UploadAsync($"/api/v1/pupils/{pupilId}/documents/TransferLetter/file", jar, "GIF89a\x01\x00\x01\x00"u8.ToArray());
        gif.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ReadRootAsync(gif)).GetProperty("errorCode").GetString().ShouldBe("pupil_document.unsupported_type");
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        RequireDatabase();
        var pupilId = await SeedPendingAdmissionAsync();

        (await GetAsync($"/api/v1/pupils/{pupilId}/photo", new CookieJar())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Seeding and HTTP helpers ----------------------------------------------------------------

    private static byte[] Image(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SteelBlue);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private async Task<List<string>> AuditActionsAsync(Guid pupilId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = pupilId.ToString("D", CultureInfo.InvariantCulture);
        return await context.AuditEvents.AsNoTracking().Where(audit => audit.EntityId == id).Select(audit => audit.Action)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A pending pupil with its admission record: no enrolment, no arm, exactly as step 1 leaves it.</summary>
    private async Task<Guid> SeedPendingAdmissionAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.SectionId == SeededClassLevels.PrimarySectionId, TestContext.Current.CancellationToken)).Id;
        var year = Interlocked.Increment(ref _nextYear);
        var session = SchoolManagement.Domain.Sessions.AcademicSession.Create(
            Guid.CreateVersion7(), $"{year}/{year + 1}", new DateOnly(year, 9, 1), new DateOnly(year + 1, 7, 30)).Value;
        context.Add(session);
        var admitted = new DateOnly(2026, 9, 10);
        var pupil = Pupil.Create(
            Guid.CreateVersion7(), "Okafor", "Chidera", middleName: null, PupilSex.Female, new DateOnly(2018, 1, 1), asOfDate: admitted,
            nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka", previousSchool: null, previousClass: null, otherInformation: null).Value;
        var record = AdmissionRecord.Create(
            Guid.CreateVersion7(), pupil.Id, session.Id, dateApplicationReceived: null, admitted, asOfDate: admitted, levelId, AdmissionType.New,
            admissionTypeNote: null, assessmentRequired: false).Value;
        context.AddRange(pupil, record);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return pupil.Id;
    }

    private async Task<CookieJar> SignInAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        using var request = new HttpRequestMessage(HttpMethod.Post, SignInUrl) { Content = JsonContent.Create(new SignInCommand(email, AdminAccountSeeder.Password)) };
        jar.ApplyWithCsrf(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private async Task<HttpResponseMessage> UploadAsync(string url, CookieJar jar, byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var form = new MultipartFormDataContent { { file, "file", "upload.bin" } };
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        jar.ApplyWithCsrf(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private async Task<HttpResponseMessage> GetAsync(string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private async Task<HttpResponseMessage> DeleteAsync(string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        jar.ApplyWithCsrf(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private static async Task<JsonElement> ReadRootAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }
}
