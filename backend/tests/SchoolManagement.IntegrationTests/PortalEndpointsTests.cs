using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Abstractions.Pins;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>Spec 6.9 portal: lookup, use counting, resume, the 6.9.4 copies, the spread control, the blocks (3a); the sheet (3b); the PDF and verification (3c).</summary>
public sealed class PortalEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static int _nextYear = 9300;
    private static int _nextSerial = 100;

    [Fact]
    public async Task Lookup_WithAValidPinAndAPublishedResult_OpensASessionAndCountsOneUse()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 3);
        using var client = NewClient();

        var stopwatch = Stopwatch.StartNew();
        var lookup = await LookupAsync(client, school.Pupils[0].TypedLoosely, pin.TypedLoosely);
        stopwatch.Stop();

        lookup.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        lookup.Headers.Location!.ToString().ShouldBe("/portal/terms");
        stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(390));
        var cookie = CookieFrom(lookup);

        var terms = await GetAsync(client, "/portal/terms", cookie);
        var html = await terms.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.ShouldContain(school.Pupils[0].Name);
        html.ShouldContain("Available");
        html.ShouldContain("You have 2 left");
        terms.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("style-src 'sha256-");
        terms.Headers.GetValues("Content-Security-Policy").Single().ShouldNotContain("script-src");

        var stored = await PinAsync(pin.Id);
        stored.UseCount.ShouldBe(1);
        stored.DistinctPupilCount.ShouldBe(1);
        stored.State.ShouldBe(PinState.Active);
    }

    [Fact]
    public async Task Lookup_AWrongPin_OrAnUnknownNumber_GetsTheSameCopy_AndSpendsNothing()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 3);
        using var client = NewClient();

        var wrongPin = await ReadAsync(await LookupAsync(client, school.Pupils[0].Number, "ABCDE-FGHJK"));
        var unknownNumber = await ReadAsync(await LookupAsync(client, "NOPE-0000", pin.Value));

        wrongPin.ShouldContain("We could not find that result");
        unknownNumber.ShouldContain("We could not find that result");
        (await PinAsync(pin.Id)).UseCount.ShouldBe(0);
    }

    [Fact]
    public async Task Lookup_WhenNothingIsPublished_SaysSo_AndSpendsNothing()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: false);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 3);
        using var client = NewClient();

        var html = await ReadAsync(await LookupAsync(client, school.Pupils[0].Number, pin.Value));

        html.ShouldContain("Results have not been released yet");
        (await PinAsync(pin.Id)).UseCount.ShouldBe(0);
    }

    [Fact]
    public async Task Lookup_AgainOnTheSameDeviceInsideTheWindow_ResumesWithoutASecondUse()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 3);
        using var client = NewClient();

        var cookie = CookieFrom(await LookupAsync(client, school.Pupils[0].Number, pin.Value));
        var again = await LookupAsync(client, school.Pupils[0].Number, pin.Value, cookie);

        again.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await PinAsync(pin.Id)).UseCount.ShouldBe(1);
    }

    [Fact]
    public async Task Lookup_APinWithNoUsesLeft_SaysItIsUsedUp()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true, pupils: 2);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 1);
        using var client = NewClient();

        (await LookupAsync(client, school.Pupils[0].Number, pin.Value)).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var html = await ReadAsync(await LookupAsync(client, school.Pupils[1].Number, pin.Value));

        html.ShouldContain("This pin has been used up");
        html.ShouldContain("used 1 times");
    }

    [Fact]
    public async Task Lookup_AThirdPupilWithinTenMinutes_SuspendsThePin()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true, pupils: 3);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 20);
        using var client = NewClient();

        (await LookupAsync(client, school.Pupils[0].Number, pin.Value)).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await LookupAsync(client, school.Pupils[1].Number, pin.Value)).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var html = await ReadAsync(await LookupAsync(client, school.Pupils[2].Number, pin.Value));

        html.ShouldContain("This pin needs to be checked");
        var stored = await PinAsync(pin.Id);
        stored.State.ShouldBe(PinState.Suspended);
        stored.UseCount.ShouldBe(2);
    }

    [Fact]
    public async Task Lookup_FiveMissesOnOneNumber_BlocksThatNumber()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 3);
        using var client = NewClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await ReadAsync(await LookupAsync(client, school.Pupils[0].Number, "WRONG-PIN22"))).ShouldContain("We could not find that result");
        }

        var html = await ReadAsync(await LookupAsync(client, school.Pupils[0].Number, pin.Value));

        html.ShouldContain("Too many tries for this pupil");
        (await PinAsync(pin.Id)).UseCount.ShouldBe(0);
    }

    [Fact]
    public async Task Session_EndsAtOnce_WhenItsPinIsRevoked()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: true);
        var pin = await SeedPinAsync(school.SessionId, maxUses: 3);
        using var client = NewClient();
        var cookie = CookieFrom(await LookupAsync(client, school.Pupils[0].Number, pin.Value));

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await context.Set<Pin>().SingleAsync(candidate => candidate.Id == pin.Id, TestContext.Current.CancellationToken);
            stored.Revoke("Lost slip", null, DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await ReadAsync(await GetAsync(client, "/portal/terms", cookie))).ShouldContain("Your session has ended");
    }

    [Fact]
    public async Task EndToEnd_APublishedResult_ShowsOnThePortal_DownloadsAsAPdf_AndVerifiesUntilWithdrawn()
    {
        RequireDatabase();
        var school = await SeedSchoolAsync(published: false);
        var pupil = school.Pupils[0];
        var (admin, adminId) = await SignInAdminAsync();
        Guid resultSetId;
        string subjectName;
        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var resultSet = await context.ResultSets.SingleAsync(set => context.Enrolments.Any(enrolment => enrolment.PupilId == pupil.Id && enrolment.ArmId == set.ArmId), TestContext.Current.CancellationToken);
            resultSetId = resultSet.Id;
            var term = await context.Terms.SingleAsync(candidate => candidate.Id == resultSet.TermId, TestContext.Current.CancellationToken);
            term.SetTimesSchoolOpened(60).IsSuccess.ShouldBeTrue();
            term.UpdateSchedule(term.Name, term.StartDate, term.EndDate, term.EndDate.AddDays(21)).IsSuccess.ShouldBeTrue();
            var arm = await context.Arms.SingleAsync(candidate => candidate.Id == resultSet.ArmId, TestContext.Current.CancellationToken);

            subjectName = $"Mathematics {Guid.NewGuid():N}"[..20];
            var subject = SchoolManagement.Domain.Subjects.Subject.Create(Guid.CreateVersion7(), subjectName, null, null).Value;
            context.Add(subject);
            context.Add(SchoolManagement.Domain.Subjects.SubjectMapping.Create(Guid.CreateVersion7(), subject.Id, arm.ClassLevelId, arm.SessionId, term.Id, 1).Value);

            var components = await context.AssessmentComponents.AsNoTracking().OrderBy(component => component.DisplayOrder).ToListAsync(TestContext.Current.CancellationToken);
            var marks = System.Text.Json.JsonSerializer.Serialize(components.Where(component => !component.IsExamination).ToDictionary(component => component.Id.ToString("D"), _ => 17));
            context.Add(SubjectScore.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, subject.Id, term.Id, marks, null, examAbsent: true).Value);
            context.Add(SubjectResultLine.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, subject.Id, 34, null, 34, "E", "Not Now", 1, false, false));
            context.Add(PupilTermResult.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, 1, 100, 34, 34.00m, "E", 1, false, 1, 1, false, 1));
            context.Add(AttendanceEntry.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, 55).Value);
            context.Add(PupilRemark.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, RemarkKind.ClassTeacher, "A steady term.", adminId, "Mrs Adeyemi", DateTimeOffset.UtcNow).Value);
            context.Add(PupilRemark.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, RemarkKind.HeadTeacher, "Keep working hard.", adminId, "Mr Bello", DateTimeOffset.UtcNow).Value);
            resultSet.MarkComputed(null, DateTimeOffset.UtcNow, 1);

            var profile = await context.Set<SchoolManagement.Domain.Settings.SchoolProfile>().SingleAsync(TestContext.Current.CancellationToken);
            profile.SetCurrentLogo(Guid.CreateVersion7());
            profile.SetCurrentSignature(Guid.CreateVersion7());
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var publish = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/result-sets/{resultSetId}/publish"))
        {
            publish.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
            admin.ApplyWithCsrf(publish);
            var published = await Client.SendAsync(publish, TestContext.Current.CancellationToken);
            published.StatusCode.ShouldBe(HttpStatusCode.OK, await published.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        var pin = await SeedPinAsync(school.SessionId, maxUses: 3);
        using var client = NewClient();
        var cookie = CookieFrom(await LookupAsync(client, pupil.Number, pin.Value));
        var termId = await TermIdAsync(resultSetId);

        var html = await ReadAsync(await GetAsync(client, $"/portal/result/{termId}", cookie));

        html.ShouldContain(subjectName);
        html.ShouldContain("ABS");
        html.ShouldContain(">17<");
        html.ShouldContain("34.00");
        html.ShouldContain("Keep working hard.");
        html.ShouldContain("A steady term.");
        html.ShouldContain("Times present</dt><dd>55");
        html.ShouldContain("Times absent</dt><dd>5");
        html.ShouldContain(", First Term</dd>");
        html.ShouldContain("Age</dt><dd>");
        html.ShouldContain($"/portal/result/{termId:D}/pdf?u=");

        // 3c: the PDF, twice (the second from cache), for the one use the lookup spent.
        foreach (var _ in new[] { 1, 2 })
        {
            using var pdf = await GetAsync(client, $"/portal/result/{termId}/pdf", cookie);
            pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
            pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
            pdf.Content.Headers.ContentDisposition!.FileNameStar!.ShouldMatch($"^{pupil.Number.Replace('/', '-')}_First-Term_[0-9]{{4}}-[0-9]{{4}}[.]pdf$");
            var bytes = await pdf.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
            bytes.Length.ShouldBeLessThan(200 * 1024);
            System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
        }

        (await PinAsync(pin.Id)).UseCount.ShouldBe(1);
        using (var noSession = await GetAsync(client, $"/portal/result/{termId}/pdf", "gras_portal=nothing"))
        {
            noSession.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        }

        // The public verification page: initials, never the full name; figures while current.
        string token;
        await using (var scope = Fixture.CreateScope())
        {
            token = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<ResultVerification>().AsNoTracking()
                .Where(verification => verification.ResultSetId == resultSetId && verification.PupilId == pupil.Id)
                .Select(verification => verification.Token).SingleAsync(TestContext.Current.CancellationToken);
        }

        using var visitor = NewClient();
        var verified = await visitor.GetStringAsync(new Uri($"/verify?code={Uri.EscapeDataString(ResultVerification.Format(token).ToLowerInvariant())}", UriKind.Relative), TestContext.Current.CancellationToken);
        verified.ShouldContain("Issued by");
        verified.ShouldContain(pupil.Number);
        verified.ShouldContain("34 of 100");
        verified.ShouldContain("34.00");
        verified.ShouldNotContain("Chidera");
        verified.ShouldNotContain(subjectName);
        (await visitor.GetStringAsync(new Uri("/verify/ABCDEFGHJKMNPQRSTUVWXY", UriKind.Relative), TestContext.Current.CancellationToken))
            .ShouldContain("No result matches this code");

        using (var withdraw = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/result-sets/{resultSetId}/withdraw"))
        {
            withdraw.Content = System.Net.Http.Json.JsonContent.Create(new { reason = "Mathematics marks were entered for the wrong class." });
            admin.ApplyWithCsrf(withdraw);
            (await Client.SendAsync(withdraw, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var afterWithdrawal = await visitor.GetStringAsync(new Uri($"/verify/{token}", UriKind.Relative), TestContext.Current.CancellationToken);
        afterWithdrawal.ShouldContain("withdrawn for correction");
        afterWithdrawal.ShouldNotContain("34.00");
    }

    private async Task<Guid> TermIdAsync(Guid resultSetId)
    {
        await using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ResultSets.AsNoTracking()
            .Where(set => set.Id == resultSetId).Select(set => set.TermId).SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<(CookieJar Jar, Guid AccountId)> SignInAdminAsync()
    {
        var (accountId, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false, email: $"admin-{Guid.NewGuid():N}@example.com");
        var jar = new CookieJar();
        using (var csrf = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf"))
        {
            jar.Capture(await Client.SendAsync(csrf, TestContext.Current.CancellationToken));
        }

        using var signIn = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-in")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new SchoolManagement.Application.Auth.SignIn.SignInCommand(email, AdminAccountSeeder.Password)),
        };
        jar.ApplyWithCsrf(signIn);
        var response = await Client.SendAsync(signIn, TestContext.Current.CancellationToken);
        jar.Capture(response);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (jar, accountId);
    }

    private HttpClient NewClient() =>
        Fixture.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private static async Task<HttpResponseMessage> LookupAsync(HttpClient client, string number, string pin, string? cookie = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/portal/lookup")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["registrationNumber"] = number, ["pin"] = pin }),
        };
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string CookieFrom(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("gras_portal=", StringComparison.Ordinal)).Split(';')[0];

    private static Task<string> ReadAsync(HttpResponseMessage response) => response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private async Task<Pin> PinAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<Pin>().AsNoTracking()
            .SingleAsync(pin => pin.Id == id, TestContext.Current.CancellationToken);
    }

    private sealed record SeededPupil(Guid Id, string Number, string TypedLoosely, string Name);

    private sealed record SeededSchool(Guid SessionId, IReadOnlyList<SeededPupil> Pupils);

    private sealed record SeededPin(Guid Id, string Value, string TypedLoosely);

    private async Task<SeededSchool> SeedSchoolAsync(bool published, int pupils = 1)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.SectionId == SeededClassLevels.PrimarySectionId, TestContext.Current.CancellationToken)).Id;

        var year = Interlocked.Increment(ref _nextYear);
        var session = AcademicSession.Create(Guid.CreateVersion7(), $"{year}/{year + 1}", new DateOnly(year, 9, 1), new DateOnly(year + 1, 7, 31)).Value;
        session.Activate();
        context.Add(session);
        var term = Term.Create(Guid.CreateVersion7(), session.Id, 1, "First Term", new DateOnly(year, 9, 1), new DateOnly(year, 12, 15)).Value;
        context.Add(term);
        var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, null).Value;
        context.Add(arm);
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), arm.Id, term.Id).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, published ? ResultSetState.Published : ResultSetState.Approved);
        context.Add(resultSet);

        var seeded = new List<SeededPupil>();
        for (var index = 0; index < pupils; index++)
        {
            var surname = new[] { "Okafor", "Eze", "Nwosu", "Obi", "Adeyemi" }[index];
            var pupil = Pupil.Create(
                Guid.CreateVersion7(), surname, "Chidera", middleName: null, PupilSex.Female, new DateOnly(2018, 1, 1), asOfDate: new DateOnly(2026, 9, 9),
                nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka", previousSchool: null, previousClass: null, otherInformation: null).Value;
            context.Add(pupil);
            context.Add(Enrolment.Open(Guid.CreateVersion7(), pupil.Id, arm.Id, new DateOnly(year, 9, 1)).Value);
            var serial = Interlocked.Increment(ref _nextSerial);
            seeded.Add(new SeededPupil(pupil.Id, $"GRAS/{year}/{serial:D4}", $"gras-{year}-{serial:D4}", $"Chidera {surname}"));
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        foreach (var pupil in seeded)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE pupils SET status = {nameof(PupilStatus.Active)}, registration_number = {pupil.Number} WHERE id = {pupil.Id}",
                TestContext.Current.CancellationToken);
        }

        return new SeededSchool(session.Id, seeded);
    }

    private async Task<SeededPin> SeedPinAsync(Guid sessionId, int maxUses)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secrets = scope.ServiceProvider.GetRequiredService<IPinSecrets>();

        var batch = PinBatch.Create(Guid.CreateVersion7(), sessionId, $"Batch {Guid.NewGuid():N}", null, 10, maxUses, 1, DateTimeOffset.UtcNow, null);
        var value = PinValue.Generate(10);
        var material = secrets.Protect(value);
        var pin = Pin.Create(Guid.CreateVersion7(), batch.Id, material.PinHash, material.LookupKey, value[..4], material.Ciphertext, maxUses);
        context.Add(batch);
        context.Add(pin);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new SeededPin(pin.Id, value, " " + PinValue.Format(value).ToLowerInvariant() + " ");
    }
}
