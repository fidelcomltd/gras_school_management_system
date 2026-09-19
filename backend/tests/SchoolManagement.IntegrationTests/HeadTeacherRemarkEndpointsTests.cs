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
/// End-to-end proof of TASK-0086 stage A's head-teacher remark endpoints (spec §6.7.7; appendix
/// C.6): ruling H's WIDER lock window (Draft through Approved) than the class-teacher and
/// attendance sheets, and the fill-all action on a MIXED arm — rows apply first, the fill never
/// overwrites an existing remark. Signs in as a real seeded Super Admin throughout.
/// </summary>
public sealed class HeadTeacherRemarkEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    [Fact]
    public async Task Save_OnceTheResultSetIsAwaitingApproval_StillSucceeds()
    {
        // Ruling H: unlike the class-teacher sheet, submission does NOT lock this one — the head
        // teacher writes it on the approval screen.
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Adeyemi");
        var jar = await SignInAsSuperAdminAsync();
        var first = await SaveSheetAsync(armId, jar, termId, version: null, rows: [Row(pupilId, "Text.")]);
        var firstBody = await ReadAsync<RemarkSheetDto>(first);
        await SetResultSetStateAsync(armId, termId, ResultSetState.AwaitingApproval);

        var response = await SaveSheetAsync(armId, jar, termId, firstBody.Version, rows: [Row(pupilId, "Revised.")]);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Save_OnceTheResultSetIsApproved_StillSucceeds()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Bello");
        var jar = await SignInAsSuperAdminAsync();
        var first = await SaveSheetAsync(armId, jar, termId, version: null, rows: [Row(pupilId, "Text.")]);
        var firstBody = await ReadAsync<RemarkSheetDto>(first);
        await SetResultSetStateAsync(armId, termId, ResultSetState.Approved);

        var response = await SaveSheetAsync(armId, jar, termId, firstBody.Version, rows: [Row(pupilId, "Revised.")]);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Save_OnceTheResultSetIsPublished_Returns409Locked()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Chukwu");
        var jar = await SignInAsSuperAdminAsync();
        await SaveSheetAsync(armId, jar, termId, version: null, rows: [Row(pupilId, "Text.")]);
        await SetResultSetStateAsync(armId, termId, ResultSetState.Published);

        var response = await SaveSheetAsync(armId, jar, termId, version: null, rows: [Row(pupilId, "Revised.")]);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("head_teacher_remarks.result_set_locked");
    }

    [Fact]
    public async Task Save_OnceTheResultSetIsWithdrawn_Returns409Locked()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Danladi");
        var jar = await SignInAsSuperAdminAsync();
        await SaveSheetAsync(armId, jar, termId, version: null, rows: [Row(pupilId, "Text.")]);
        await SetResultSetStateAsync(armId, termId, ResultSetState.Withdrawn);

        var response = await SaveSheetAsync(armId, jar, termId, version: null, rows: [Row(pupilId, "Revised.")]);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("head_teacher_remarks.result_set_locked");
    }

    [Fact]
    public async Task Save_FillEmpty_OnAMixedArm_FillsOnlyThePupilsWithNoRemark()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var writtenPupilId = await SeedPupilOnRosterAsync(armId, "Efe");
        var blankPupilId1 = await SeedPupilOnRosterAsync(armId, "Femi");
        var blankPupilId2 = await SeedPupilOnRosterAsync(armId, "Grace");
        var jar = await SignInAsSuperAdminAsync();

        var first = await SaveSheetAsync(armId, jar, termId, version: null, fillEmpty: null, rows: [Row(writtenPupilId, "Already written.")]);
        var firstBody = await ReadAsync<RemarkSheetDto>(first);

        var second = await SaveSheetAsync(armId, jar, termId, firstBody.Version, fillEmpty: "Keep up the good work.");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondBody = await ReadAsync<RemarkSheetDto>(second);
        secondBody.Rows.Single(r => r.PupilId == writtenPupilId.ToString()).Remark.ShouldBe("Already written.");
        secondBody.Rows.Single(r => r.PupilId == blankPupilId1.ToString()).Remark.ShouldBe("Keep up the good work.");
        secondBody.Rows.Single(r => r.PupilId == blankPupilId2.ToString()).Remark.ShouldBe("Keep up the good work.");
    }

    [Fact]
    public async Task Save_RowsAndFillEmptyTogether_RowsApplyBeforeTheFill()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var rowPupilId = await SeedPupilOnRosterAsync(armId, "Halima");
        var fillPupilId = await SeedPupilOnRosterAsync(armId, "Ibrahim");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(
            armId, jar, termId, version: null, fillEmpty: "Generic fill text.",
            rows: [Row(rowPupilId, "Specific remark from the rows.")]);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<RemarkSheetDto>(response);
        body.Rows.Single(r => r.PupilId == rowPupilId.ToString()).Remark.ShouldBe("Specific remark from the rows.");
        body.Rows.Single(r => r.PupilId == fillPupilId.ToString()).Remark.ShouldBe("Generic fill text.");
    }

    [Fact]
    public async Task Save_FillEmptyOverThreeHundredCharacters_Returns422()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        await SeedPupilOnRosterAsync(armId, "Joy");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(armId, jar, termId, version: null, fillEmpty: new string('a', 301));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // ---- Seeding and HTTP helpers ----------------------------------------------------------------

    private static int _nextSessionStartYear = 8000;

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

    private Task<HttpResponseMessage> SaveSheetAsync(
        Guid armId, CookieJar jar, Guid termId, string? version, string? fillEmpty = null, params object[] rows) =>
        Client.SendAsync(
            BuildPutRequest(
                $"/api/v1/arms/{armId}/head-teacher-remarks", jar,
                new { termId = termId.ToString(), version, rows, fillEmpty }),
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
