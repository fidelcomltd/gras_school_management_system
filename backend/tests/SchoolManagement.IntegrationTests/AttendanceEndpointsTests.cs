using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0086 stage A's attendance endpoints (spec §6.7.7, §6.7.12 amendment):
/// ruling A's derived <c>timesAbsent</c>, the omitted-vs-null row semantics, the first-save state
/// transition, staleness, the lock states, and the 422 upper-bound rule. Signs in as a real seeded
/// Super Admin throughout — every route's privilege is arm-scoped, and a Super Admin holds every
/// privilege school-wide.
/// </summary>
public sealed class AttendanceEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    // ---- GET ---------------------------------------------------------------------------------

    [Fact]
    public async Task Get_WithNothingEnteredAtAll_ReturnsEveryActivePupilBlank()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        await SeedPupilOnRosterAsync(armId, "Adeyemi");
        var jar = await SignInAsSuperAdminAsync();

        var response = await GetSheetAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<AttendanceSheetDto>(response);
        body.Version.ShouldBeNull();
        body.ResultSet.ShouldBeNull();
        body.TimesSchoolOpened.ShouldBeNull();
        body.Rows.Count.ShouldBe(1);
        body.Rows[0].TimesPresent.ShouldBeNull();
        body.Rows[0].TimesAbsent.ShouldBeNull();
    }

    [Fact]
    public async Task Get_ForANurseryArm_Succeeds()
    {
        // Spec §6.7.12 amendment / conflict 7: attendance is entered for a nursery arm too, even
        // though it is not printed there — unlike trait ratings, there is no section gate here.
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.NurserySectionId);
        var jar = await SignInAsSuperAdminAsync();

        var response = await GetSheetAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---- Save: first-save transition, Q1-A-style cell semantics --------------------------------

    [Fact]
    public async Task Save_FirstSaveForTheArm_CreatesTheResultSetInDraftWithNeedsRecomputeTrue()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Bello");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, 58));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<AttendanceSheetDto>(response);
        body.ResultSet.ShouldNotBeNull();
        body.ResultSet!.State.ShouldBe(ResultSetState.Draft);
        body.ResultSet!.NeedsRecompute.ShouldBeTrue();
        body.Version.ShouldNotBeNull();
        body.Rows.Single().TimesPresent.ShouldBe(58);
    }

    [Fact]
    public async Task Save_AnOmittedPupil_LeavesAPreviouslySavedEntryUntouched()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Chukwu");
        var otherPupilId = await SeedPupilOnRosterAsync(armId, "Danladi");
        var jar = await SignInAsSuperAdminAsync();

        var first = await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, 58), Row(otherPupilId, 40));
        var firstBody = await ReadAsync<AttendanceSheetDto>(first);

        // Second save touches ONLY pupilId — otherPupilId's key is entirely absent from the payload.
        var second = await SaveSheetAsync(armId, jar, termId, firstBody.Version, Row(pupilId, 60));

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondBody = await ReadAsync<AttendanceSheetDto>(second);
        secondBody.Rows.Single(r => r.PupilId == pupilId.ToString()).TimesPresent.ShouldBe(60);
        secondBody.Rows.Single(r => r.PupilId == otherPupilId.ToString()).TimesPresent.ShouldBe(40);
    }

    [Fact]
    public async Task Save_AnExplicitNull_ClearsAPreviouslySavedEntryAndDeletesTheRow()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Efe");
        var jar = await SignInAsSuperAdminAsync();

        var first = await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, 58));
        var firstBody = await ReadAsync<AttendanceSheetDto>(first);

        var second = await SaveSheetAsync(
            armId, jar, termId, firstBody.Version, new { pupilId = pupilId.ToString(), timesPresent = (int?)null });

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondBody = await ReadAsync<AttendanceSheetDto>(second);
        secondBody.Version.ShouldBeNull();
        secondBody.Rows.Single().TimesPresent.ShouldBeNull();

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Set<AttendanceEntry>().AsNoTracking().AnyAsync(
            entry => entry.PupilId == pupilId, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Save_WithAStaleVersion_Returns409()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Femi");
        var jar = await SignInAsSuperAdminAsync();

        await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, 58));

        var retry = await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, 40));

        retry.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(retry);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("attendance.stale_version");
    }

    [Fact]
    public async Task Save_OnceTheResultSetIsApproved_Returns409Locked()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Grace");
        var jar = await SignInAsSuperAdminAsync();
        await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, 58));
        await SetResultSetStateAsync(armId, termId, ResultSetState.Approved);

        var response = await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, 40));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("attendance.result_set_locked");
    }

    // ---- Save: per-row validation ----------------------------------------------------------------

    [Fact]
    public async Task Save_ANegativeTimesPresent_Returns422()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Halima");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, -1));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").ToString().ShouldContain("not be negative");
    }

    [Fact]
    public async Task Save_ATimesPresentAboveTheTermsTimesSchoolOpened_Returns422()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId, timesSchoolOpened: 58);
        var pupilId = await SeedPupilOnRosterAsync(armId, "Ibrahim");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, 59));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").ToString().ShouldContain("cannot exceed 58");
    }

    [Fact]
    public async Task Save_APupilNotOnTheRoster_Returns422()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync(SeededClassLevels.PrimarySectionId);
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(armId, jar, termId, version: null, Row(Guid.CreateVersion7(), 10));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").ToString().ShouldContain("not on the arm's active roster");
    }

    // ---- Seeding and HTTP helpers ----------------------------------------------------------------

    private static int _nextSessionStartYear = 6000;

    private static object Row(Guid pupilId, int? timesPresent) => new { pupilId = pupilId.ToString(), timesPresent };

    /// <summary>Seeds an active session, an open (Upcoming) term, and one arm under a level belonging to <paramref name="sectionId"/>.</summary>
    private async Task<(Guid ArmId, Guid TermId, Guid SessionId)> SeedArmAsync(Guid sectionId, int? timesSchoolOpened = null)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.SectionId == sectionId, TestContext.Current.CancellationToken)).Id;

        var startYear = Interlocked.Increment(ref _nextSessionStartYear);
        var session = SchoolManagement.Domain.Sessions.AcademicSession.Create(
            Guid.CreateVersion7(), $"{startYear}/{startYear + 1}",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25)).Value;
        session.Activate();
        context.Add(session);

        var term = Term.Create(
            Guid.CreateVersion7(), session.Id, 1, "First Term",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear, 12, 12)).Value;

        if (timesSchoolOpened is { } opened)
        {
            term.SetTimesSchoolOpened(opened);
        }

        context.Add(term);

        var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, null).Value;
        context.Add(arm);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (arm.Id, term.Id, session.Id);
    }

    private async Task<Guid> SeedPupilOnRosterAsync(Guid armId, string surname)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pupil = Pupil.Create(
            Guid.CreateVersion7(), surname, "Chidera", middleName: null, PupilSex.Female,
            new DateOnly(2018, 1, 1), asOfDate: new DateOnly(2026, 9, 9), nationality: null,
            "Anambra", "Awka South", "14 Zik Avenue, Awka",
            previousSchool: null, previousClass: null, otherInformation: null).Value;
        context.Add(pupil);

        var enrolment = SchoolManagement.Domain.Enrolments.Enrolment.Open(
            Guid.CreateVersion7(), pupil.Id, armId, new DateOnly(2026, 9, 14)).Value;
        context.Add(enrolment);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET status = {nameof(PupilStatus.Active)} WHERE id = {pupil.Id}",
            TestContext.Current.CancellationToken);
        return pupil.Id;
    }

    /// <summary>Sets the result set's state directly — no state-machine endpoint exists yet (later cards).</summary>
    private async Task SetResultSetStateAsync(Guid armId, Guid termId, ResultSetState state)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.SingleAsync(
            r => r.ArmId == armId && r.TermId == termId, TestContext.Current.CancellationToken);
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, state);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<CookieJar> SignInAsSuperAdminAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsyncCore(CsrfUrl, jar);
        var signIn = await PostAsyncCore(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private Task<HttpResponseMessage> GetSheetAsync(Guid armId, Guid termId, CookieJar jar) =>
        GetAsyncCore($"/api/v1/arms/{armId}/attendance?termId={termId}", jar);

    private Task<HttpResponseMessage> SaveSheetAsync(Guid armId, CookieJar jar, Guid termId, string? version, params object[] rows) =>
        Client.SendAsync(
            BuildPutRequest($"/api/v1/arms/{armId}/attendance", jar, new { termId = termId.ToString(), version, rows }),
            TestContext.Current.CancellationToken);

    private static HttpRequestMessage BuildPutRequest<T>(string url, CookieJar jar, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(payload),
        };
        jar.ApplyWithCsrf(request);
        return request;
    }

    private Task<HttpResponseMessage> GetAsyncCore(string url, CookieJar jar) => SendAsyncCore(Client, HttpMethod.Get, url, jar, jar.Apply);

    private Task<HttpResponseMessage> PostAsyncCore<T>(string url, CookieJar jar, T payload) =>
        SendWithCsrfAsync(Client, HttpMethod.Post, url, jar, payload);

    private static async Task<HttpResponseMessage> SendAsyncCore(
        HttpClient client, HttpMethod method, string url, CookieJar jar, Action<HttpRequestMessage> apply)
    {
        using var request = new HttpRequestMessage(method, url);
        apply(request);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private static async Task<HttpResponseMessage> SendWithCsrfAsync<T>(
        HttpClient client, HttpMethod method, string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(payload) };
        jar.ApplyWithCsrf(request);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
