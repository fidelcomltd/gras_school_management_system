using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Abstractions.Pins;
using SchoolManagement.Application.Results.Annual;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>Spec 6.7.10: <c>POST /api/v1/arms/{armId}/annual-results</c> over three published terms (8.4.9's figures), then the portal's annual page and PDF.</summary>
public sealed class AnnualResultEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static int _nextYear = 9500;
    private static int _nextSerial = 500;

    [Fact]
    public async Task Compute_AfterThreePublishedTerms_WritesTheCumulativeAverage_GradeAndProposal()
    {
        RequireDatabase();
        var seeded = await SeedAsync(secondTermState: ResultSetState.Published);
        var jar = await SignInAdminAsync();

        var response = await PostAsync($"/api/v1/arms/{seeded.ArmId}/annual-results", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var body = await response.Content.ReadFromJsonAsync<ComputeAnnualResultsResponse>(TestContext.Current.CancellationToken);
        body!.PupilCount.ShouldBe(1);
        body.RankedCount.ShouldBe(1);
        body.ProposedPromoted.ShouldBe(1);

        await using var scope = Fixture.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AnnualResult>().AsNoTracking()
            .SingleAsync(result => result.PupilId == seeded.PupilId, TestContext.Current.CancellationToken);
        row.CumulativeAverage.ShouldBe(80.25m);
        row.CumulativeGrade.ShouldBe("B");
        row.TermsCounted.ShouldBe(3);
        row.GrandTotal.ShouldBe(80 + 78 + 83);
        row.AnnualPosition.ShouldBe(1);
        row.ProposedOutcome.ShouldBe(PromotionOutcome.Promoted);
        row.SubjectsJson.ShouldContain("80.33");

        // Idempotent: a second run rewrites rather than duplicating.
        (await PostAsync($"/api/v1/arms/{seeded.ArmId}/annual-results", jar)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AnnualResult>().CountAsync(result => result.PupilId == seeded.PupilId, TestContext.Current.CancellationToken))
            .ShouldBe(1);

        // 3d-2: the parent sees it inside the viewing session, and downloads it for no extra use.
        var pin = await SeedPinAsync(seeded.SessionId);
        using var parent = Fixture.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        using var lookup = new HttpRequestMessage(HttpMethod.Post, "/portal/lookup")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["registrationNumber"] = seeded.Number, ["pin"] = pin.Value }),
        };
        var opened = await parent.SendAsync(lookup, TestContext.Current.CancellationToken);
        opened.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var cookie = opened.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("gras_portal=", StringComparison.Ordinal)).Split(';')[0];

        (await GetPortalAsync(parent, "/portal/terms", cookie)).ShouldContain($"/portal/annual/{seeded.SessionId:D}?u=");
        var page = await GetPortalAsync(parent, $"/portal/annual/{seeded.SessionId}", cookie);
        page.ShouldContain("80.25");
        page.ShouldContain("80.33");
        page.ShouldContain("Very good");

        using var download = new HttpRequestMessage(HttpMethod.Get, $"/portal/annual/{seeded.SessionId}/pdf");
        download.Headers.Add("Cookie", cookie);
        using var pdf = await parent.SendAsync(download, TestContext.Current.CancellationToken);
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        pdf.Content.Headers.ContentDisposition!.FileNameStar!.ShouldContain("_Annual_");
        (await pdf.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Length.ShouldBeLessThan(200 * 1024);
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<Pin>().AsNoTracking()
            .SingleAsync(candidate => candidate.Id == pin.Id, TestContext.Current.CancellationToken)).UseCount.ShouldBe(1);
    }

    [Fact]
    public async Task Compute_WithAnUnpublishedTerm_Is409_NamingIt()
    {
        RequireDatabase();
        var seeded = await SeedAsync(secondTermState: ResultSetState.Approved);
        var jar = await SignInAdminAsync();

        var response = await PostAsync($"/api/v1/arms/{seeded.ArmId}/annual-results", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        problem.ShouldContain("Second Term results for ");
        problem.ShouldContain("are not published. Publish all three terms before computing annual results.");
    }

    private sealed record Seeded(Guid ArmId, Guid PupilId, Guid SessionId, string Number);

    private async Task<Seeded> SeedAsync(ResultSetState secondTermState)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.SectionId == SeededClassLevels.PrimarySectionId, TestContext.Current.CancellationToken)).Id;

        var year = Interlocked.Increment(ref _nextYear);
        var session = AcademicSession.Create(Guid.CreateVersion7(), $"{year}/{year + 1}", new DateOnly(year, 9, 1), new DateOnly(year + 1, 7, 31)).Value;
        session.Activate();
        context.Add(session);
        var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, null).Value;
        context.Add(arm);
        var pupil = Pupil.Create(
            Guid.CreateVersion7(), "Okafor", "Adaeze", middleName: null, PupilSex.Female, new DateOnly(2018, 3, 4), asOfDate: new DateOnly(2026, 9, 9),
            nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka", previousSchool: null, previousClass: null, otherInformation: null).Value;
        context.Add(pupil);
        context.Add(Enrolment.Open(Guid.CreateVersion7(), pupil.Id, arm.Id, new DateOnly(year, 9, 1)).Value);
        var subject = SchoolManagement.Domain.Subjects.Subject.Create(Guid.CreateVersion7(), $"English {Guid.NewGuid():N}"[..20], null, null).Value;
        context.Add(subject);

        const string Snapshot = """{"settings":{"grading":{"bands":[{"gradeLetter":"A","lowerBound":90,"upperBound":100,"remark":"Excellent","displayOrder":1},{"gradeLetter":"B","lowerBound":80,"upperBound":89,"remark":"Very good","displayOrder":2},{"gradeLetter":"C","lowerBound":0,"upperBound":79,"remark":"Credit","displayOrder":3}]}}}""";
        (int Ordinal, string Name, decimal Average, int Total, ResultSetState State)[] terms =
        [
            (1, "First Term", 80.00m, 80, ResultSetState.Published),
            (2, "Second Term", 78.25m, 78, secondTermState),
            (3, "Third Term", 82.50m, 83, ResultSetState.Published),
        ];
        foreach (var (ordinal, name, average, total, state) in terms)
        {
            var term = Term.Create(Guid.CreateVersion7(), session.Id, ordinal, name, new DateOnly(year, 9, 1).AddMonths(3 * (ordinal - 1)), new DateOnly(year, 11, 20).AddMonths(3 * (ordinal - 1))).Value;
            context.Add(term);
            var resultSet = ResultSet.Create(Guid.CreateVersion7(), arm.Id, term.Id).Value;
            typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, state);
            typeof(ResultSet).GetProperty(nameof(ResultSet.ConfigSnapshotJson))!.SetValue(resultSet, Snapshot);
            typeof(ResultSet).GetProperty(nameof(ResultSet.PublishedAtUtc))!.SetValue(resultSet, DateTimeOffset.UtcNow);
            context.Add(resultSet);
            context.Add(SubjectResultLine.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, subject.Id, 30, total - 30, total, "B", "Very good", 1, false, true));
            context.Add(PupilTermResult.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, 1, 100, total, average, "B", 1, false, 1, 1, false, 1));
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var number = $"GRAS/{year}/{Interlocked.Increment(ref _nextSerial):D4}";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET status = {nameof(PupilStatus.Active)}, registration_number = {number} WHERE id = {pupil.Id}",
            TestContext.Current.CancellationToken);
        return new Seeded(arm.Id, pupil.Id, session.Id, number);
    }

    private async Task<(Guid Id, string Value)> SeedPinAsync(Guid sessionId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var value = PinValue.Generate(10);
        var material = scope.ServiceProvider.GetRequiredService<IPinSecrets>().Protect(value);
        var batch = PinBatch.Create(Guid.CreateVersion7(), sessionId, $"Batch {Guid.NewGuid():N}", null, 10, 3, 1, DateTimeOffset.UtcNow, null);
        var pin = Pin.Create(Guid.CreateVersion7(), batch.Id, material.PinHash, material.LookupKey, value[..4], material.Ciphertext, 3);
        context.Add(batch);
        context.Add(pin);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (pin.Id, value);
    }

    private static async Task<string> GetPortalAsync(HttpClient client, string url, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", cookie);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private async Task<CookieJar> SignInAdminAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false, email: $"admin-{Guid.NewGuid():N}@example.com");
        var jar = new CookieJar();
        using (var csrf = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf"))
        {
            jar.Capture(await Client.SendAsync(csrf, TestContext.Current.CancellationToken));
        }

        using var signIn = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-in")
        {
            Content = JsonContent.Create(new SchoolManagement.Application.Auth.SignIn.SignInCommand(email, AdminAccountSeeder.Password)),
        };
        jar.ApplyWithCsrf(signIn);
        var response = await Client.SendAsync(signIn, TestContext.Current.CancellationToken);
        jar.Capture(response);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private async Task<HttpResponseMessage> PostAsync(string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        jar.ApplyWithCsrf(request);
        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
