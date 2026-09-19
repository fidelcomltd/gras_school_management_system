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
/// End-to-end proof of TASK-0086 stage A's class-teacher remark endpoints (spec §6.7.7; appendix
/// C.6): trim/clear semantics, ruling L's 300-character limit, the first-save state transition,
/// staleness, the lock states, and that the audit trail never carries the remark's text. Signs in
/// as a real seeded Super Admin throughout.
/// </summary>
public sealed class ClassTeacherRemarkEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    [Fact]
    public async Task Get_WithNothingWrittenAtAll_ReturnsEveryActivePupilBlank()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        await SeedPupilOnRosterAsync(armId, "Adeyemi");
        var jar = await SignInAsSuperAdminAsync();

        var response = await GetSheetAsync(armId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<RemarkSheetDto>(response);
        body.Version.ShouldBeNull();
        body.Rows.Single().Remark.ShouldBeNull();
    }

    [Fact]
    public async Task Save_FirstSaveForTheArm_CreatesTheResultSetInDraftAndTrimsText()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Bello");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, "  A diligent pupil.  "));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<RemarkSheetDto>(response);
        body.ResultSet.ShouldNotBeNull();
        body.ResultSet!.State.ShouldBe(ResultSetState.Draft);
        var row = body.Rows.Single();
        row.Remark.ShouldBe("A diligent pupil.");
        row.WrittenByName.ShouldNotBeNullOrWhiteSpace();
        row.WrittenAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Save_AWhitespaceOnlyRemark_ClearsAPreviouslySavedRowAndDeletesIt()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Chukwu");
        var jar = await SignInAsSuperAdminAsync();

        var first = await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, "Some text."));
        var firstBody = await ReadAsync<RemarkSheetDto>(first);

        var second = await SaveSheetAsync(armId, jar, termId, firstBody.Version, Row(pupilId, "   "));

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondBody = await ReadAsync<RemarkSheetDto>(second);
        secondBody.Version.ShouldBeNull();
        secondBody.Rows.Single().Remark.ShouldBeNull();

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Set<PupilRemark>().AsNoTracking().AnyAsync(
            remark => remark.PupilId == pupilId, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Save_RemarkOverThreeHundredCharacters_Returns422()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Danladi");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, new string('a', 301)));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").ToString().ShouldContain("300 characters or fewer");
    }

    [Fact]
    public async Task Save_RemarkAtExactlyThreeHundredCharacters_Succeeds()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Efe");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, new string('a', 300)));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Save_WithAStaleVersion_Returns409()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Femi");
        var jar = await SignInAsSuperAdminAsync();

        await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, "First."));

        var retry = await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, "Second."));

        retry.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(retry);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("class_teacher_remarks.stale_version");
    }

    [Fact]
    public async Task Save_OnceTheResultSetIsAwaitingApproval_Returns409Locked()
    {
        // Unlike the head-teacher sheet (ruling H), the class-teacher remark locks at submission,
        // exactly like attendance and trait ratings (spec §6.7.11: "submission locks them").
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Grace");
        var jar = await SignInAsSuperAdminAsync();
        await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, "Text."));
        await SetResultSetStateAsync(armId, termId, ResultSetState.AwaitingApproval);

        var response = await SaveSheetAsync(armId, jar, termId, version: null, Row(pupilId, "Other text."));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("class_teacher_remarks.result_set_locked");
    }

    [Fact]
    public async Task Save_APupilNotOnTheRoster_Returns422()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(armId, jar, termId, version: null, Row(Guid.CreateVersion7(), "Text."));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").ToString().ShouldContain("not on the arm's active roster");
    }

    // ---- Seeding and HTTP helpers ----------------------------------------------------------------

    private static int _nextSessionStartYear = 7000;

    private static object Row(Guid pupilId, string? remark) => new { pupilId = pupilId.ToString(), remark };

    private async Task<(Guid ArmId, Guid TermId, Guid SessionId)> SeedArmAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.SectionId == SeededClassLevels.PrimarySectionId, TestContext.Current.CancellationToken)).Id;

        var startYear = Interlocked.Increment(ref _nextSessionStartYear);
        var session = SchoolManagement.Domain.Sessions.AcademicSession.Create(
            Guid.CreateVersion7(), $"{startYear}/{startYear + 1}",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25)).Value;
        session.Activate();
        context.Add(session);

        var term = Term.Create(
            Guid.CreateVersion7(), session.Id, 1, "First Term",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear, 12, 12)).Value;
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
        GetAsyncCore($"/api/v1/arms/{armId}/class-teacher-remarks?termId={termId}", jar);

    private Task<HttpResponseMessage> SaveSheetAsync(Guid armId, CookieJar jar, Guid termId, string? version, params object[] rows) =>
        Client.SendAsync(
            BuildPutRequest($"/api/v1/arms/{armId}/class-teacher-remarks", jar, new { termId = termId.ToString(), version, rows }),
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
