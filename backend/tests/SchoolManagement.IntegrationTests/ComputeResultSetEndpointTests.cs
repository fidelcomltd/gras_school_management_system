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
using SchoolManagement.Domain.Subjects;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Configurations;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0071's <c>POST /result-sets/{resultSetId}/compute</c> (spec 8.2): the
/// handler's own responsibilities (state gate, persistence, audit) over the pure engine
/// (<c>ResultComputationEngineTests</c>, unit-level). Signs in as a real seeded Super Admin — the
/// route's privilege is ResultSet-scoped, and a Super Admin holds every privilege school-wide, the
/// same posture <c>ScoreSheetEndpointsTests</c> takes.
/// </summary>
public sealed class ComputeResultSetEndpointTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private static readonly Guid Ca1Id = AssessmentComponentConfiguration.SeededIds[0];
    private static readonly Guid Ca2Id = AssessmentComponentConfiguration.SeededIds[1];
    private static int _nextSessionStartYear = 8000;

    [Fact]
    public async Task Compute_HappyPath_ReturnsSummary_AndPersistsTheThreeComputedTables()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedArmWithSubjectAsync();
        var jar = await SignInAsSuperAdminAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Okafor");
        await SaveSheetAsync(armId, jar, subjectId, termId, version: null, RowWithMarks(pupilId, 18, 16, 52)); // 86 -> A

        var resultSetId = await GetResultSetIdAsync(armId, termId);

        var response = await ComputeAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ComputeResultSetResponse>(response);
        body.ResultSetId.ShouldBe(resultSetId.ToString());
        body.PupilCount.ShouldBe(1);
        body.SubjectCount.ShouldBe(1);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var line = await context.Set<SubjectResultLine>().AsNoTracking()
            .SingleAsync(l => l.ResultSetId == resultSetId, TestContext.Current.CancellationToken);
        line.SubjectTotal.ShouldBe(86);
        line.Grade.ShouldBe("A");
        line.IsPass.ShouldBeTrue();
        line.SubjectPosition.ShouldBe(1);

        var statistic = await context.Set<SubjectArmStatistic>().AsNoTracking()
            .SingleAsync(s => s.ResultSetId == resultSetId, TestContext.Current.CancellationToken);
        statistic.HighestScore.ShouldBe(86);
        statistic.ClassAverage.ShouldBe(86.0m);

        var pupilResult = await context.Set<PupilTermResult>().AsNoTracking()
            .SingleAsync(p => p.ResultSetId == resultSetId, TestContext.Current.CancellationToken);
        pupilResult.TotalObtained.ShouldBe(86);
        pupilResult.Average.ShouldBe(86.00m);
        pupilResult.OverallGrade.ShouldBe("A");
        pupilResult.ArmPosition.ShouldBe(1);

        var resultSet = await context.ResultSets.AsNoTracking()
            .SingleAsync(r => r.Id == resultSetId, TestContext.Current.CancellationToken);
        resultSet.NeedsRecompute.ShouldBeFalse();
        resultSet.ComputedAtUtc.ShouldNotBeNull();
        resultSet.PupilCount.ShouldBe(1);
    }

    [Fact]
    public async Task Compute_RunningTwiceOnUnchangedInputs_ProducesIdenticalRows()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedArmWithSubjectAsync();
        var jar = await SignInAsSuperAdminAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Bello");
        await SaveSheetAsync(armId, jar, subjectId, termId, version: null, RowWithMarks(pupilId, 15, 15, 40)); // 70 -> B-
        var resultSetId = await GetResultSetIdAsync(armId, termId);

        var first = await ComputeAsync(resultSetId, jar);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = await ComputeAsync(resultSetId, jar);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var lines = await context.Set<SubjectResultLine>().AsNoTracking()
            .Where(l => l.ResultSetId == resultSetId).ToListAsync(TestContext.Current.CancellationToken);
        lines.Count.ShouldBe(1); // not doubled — the first run's rows were deleted, not accumulated
        lines[0].SubjectTotal.ShouldBe(70);
        lines[0].Grade.ShouldBe("B-");

        var pupilResults = await context.Set<PupilTermResult>().AsNoTracking()
            .Where(p => p.ResultSetId == resultSetId).ToListAsync(TestContext.Current.CancellationToken);
        pupilResults.Count.ShouldBe(1);
        pupilResults[0].Average.ShouldBe(70.00m);
    }

    [Fact]
    public async Task Compute_OnAPublishedResultSet_Returns409()
    {
        RequireDatabase();
        var (armId, _, termId, _) = await SeedArmWithSubjectAsync();
        var resultSetId = await SeedBareResultSetAsync(armId, termId, ResultSetState.Published);
        var jar = await SignInAsSuperAdminAsync();

        var response = await ComputeAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("result_set.published");
    }

    [Fact]
    public async Task Compute_OnAWithdrawnResultSet_Returns409()
    {
        RequireDatabase();
        var (armId, _, termId, _) = await SeedArmWithSubjectAsync();
        var resultSetId = await SeedBareResultSetAsync(armId, termId, ResultSetState.Withdrawn);
        var jar = await SignInAsSuperAdminAsync();

        var response = await ComputeAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("result_set.published");
    }

    // HUMAN RULING (2026-09-18): an unknown resultSetId stays 403, and the contract no longer
    // documents an unreachable 404 (ResultSetEndpoints.cs dropped .ProducesProblem(404)). This route
    // is the FIRST to declare ScopeParameterKind.ResultSet, and PrivilegeDecision.IsAuthorized fails
    // CLOSED on ScopeResolution.Unresolvable for every caller, Super Admin included ("An unresolvable
    // target must never be treated as in-scope" — PrivilegeDecision.cs) — so an id
    // IResultSetArmLookup cannot resolve is rejected by authorization BEFORE the handler ever runs.
    // The handler's ComputeResultSetHandler.NotFound branch stays as defence in depth: it is real and
    // reachable only for a resultSetId a caller is scoped to but which has since been deleted
    // (impossible today — nothing deletes a result_set). Against a wholly unknown id, as every other
    // caller in this codebase would supply, the response is 403, not 404.
    [Fact]
    public async Task Compute_UnknownResultSetId_Returns403_ScopeResolutionFailsClosedBeforeTheHandlersOwn404()
    {
        RequireDatabase();
        var jar = await SignInAsSuperAdminAsync();

        var response = await ComputeAsync(Guid.CreateVersion7(), jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Compute_NoActivePupils_Returns422()
    {
        RequireDatabase();
        var (armId, _, termId, _) = await SeedArmWithSubjectAsync(); // subject mapped, nobody enrolled
        var resultSetId = await SeedBareResultSetAsync(armId, termId, ResultSetState.Draft);
        var jar = await SignInAsSuperAdminAsync();

        var response = await ComputeAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("result_set.no_active_pupils");
    }

    [Fact]
    public async Task Compute_NoSubjectsInEffect_Returns422()
    {
        RequireDatabase();
        var (armId, termId) = await SeedArmWithoutSubjectAsync();
        await SeedPupilOnRosterAsync(armId, "Chukwu");
        var resultSetId = await SeedBareResultSetAsync(armId, termId, ResultSetState.Draft);
        var jar = await SignInAsSuperAdminAsync();

        var response = await ComputeAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("result_set.no_subjects_in_effect");
    }

    [Fact]
    public async Task Compute_NoCompleteScoreRows_Returns422()
    {
        RequireDatabase();
        var (armId, _, termId, _) = await SeedArmWithSubjectAsync();
        await SeedPupilOnRosterAsync(armId, "Danladi");
        var resultSetId = await SeedBareResultSetAsync(armId, termId, ResultSetState.Draft); // no marks at all
        var jar = await SignInAsSuperAdminAsync();

        var response = await ComputeAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("result_set.no_complete_scores");
    }

    [Fact]
    public async Task Compute_AuditsTheComputationWithPupilAndSubjectCounts()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedArmWithSubjectAsync();
        var jar = await SignInAsSuperAdminAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Emeka");
        await SaveSheetAsync(armId, jar, subjectId, termId, version: null, RowWithMarks(pupilId, 20, 20, 60)); // 100
        var resultSetId = await GetResultSetIdAsync(armId, termId);

        var response = await ComputeAsync(resultSetId, jar);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var events = await context.Set<Domain.Audit.AuditEvent>().AsNoTracking()
            .Where(e => e.Action == "result.compute" && e.EntityId == resultSetId.ToString())
            .ToListAsync(TestContext.Current.CancellationToken);
        events.Count.ShouldBe(1);
        events[0].AfterJson.ShouldNotBeNull();
        events[0].AfterJson!.ShouldContain("pupilCount");
    }

    // ---- Human ruling 1: level position from every arm's live marks, written for own arm only,
    // sibling's stored rows untouched byte-for-byte -------------------------------------------------

    [Fact]
    public async Task LevelPosition_FollowsASiblingArmsLiveMarksOnRecompute_WithoutTouchingTheSiblingsStoredRows()
    {
        RequireDatabase();
        // ONE subject mapped to the shared LEVEL, so both sibling arms take exactly the same one
        // subject (spec 8.1: subjects in effect come from the level mapping) — this keeps
        // subjects_taken at 1 for both arms and isolates the assertion to level ranking alone.
        var (armA, subjectId, termId, levelId, sessionId) = await SeedArmWithSubjectFullAsync();
        var armB = await SeedSiblingArmAsync(levelId, sessionId);

        var jar = await SignInAsSuperAdminAsync();

        var pupilA = await SeedPupilOnRosterAsync(armA, "Adaeze");
        await SaveSheetAsync(armA, jar, subjectId, termId, version: null, RowWithMarks(pupilA, 20, 20, 40)); // 80
        var resultSetA = await GetResultSetIdAsync(armA, termId);

        var pupilB = await SeedPupilOnRosterAsync(armB, "Chidi");
        await SaveSheetAsync(armB, jar, subjectId, termId, version: null, RowWithMarks(pupilB, 10, 10, 40)); // 60
        var resultSetB = await GetResultSetIdAsync(armB, termId);

        // Establish the baseline: A leads (80 > 60), so A is 1st of 2 and B is 2nd of 2 at the level.
        (await ComputeAsync(resultSetA, jar)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ComputeAsync(resultSetB, jar)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var baselineScope = Fixture.CreateScope();
        var baselineContext = baselineScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var aResultBefore = await baselineContext.Set<PupilTermResult>().AsNoTracking()
            .SingleAsync(p => p.ResultSetId == resultSetA, TestContext.Current.CancellationToken);
        aResultBefore.LevelPosition.ShouldBe(1);
        var bResultBaseline = await baselineContext.Set<PupilTermResult>().AsNoTracking()
            .SingleAsync(p => p.ResultSetId == resultSetB, TestContext.Current.CancellationToken);
        bResultBaseline.LevelPosition.ShouldBe(2);

        // B's marks change to overtake A (90 > 80) — B is NOT recomputed.
        var bVersion = (await ReadAsync<ScoreSheetDto>(await GetSheetAsync(armB, subjectId, termId, jar))).Version;
        await SaveSheetAsync(armB, jar, subjectId, termId, bVersion, RowWithMarks(pupilB, 20, 20, 50)); // 90

        // Recompute A only. Level position must follow B's LIVE marks even though B's own stored
        // computed rows are stale.
        var recompute = await ComputeAsync(resultSetA, jar);
        recompute.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var afterScope = Fixture.CreateScope();
        var afterContext = afterScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var aResultAfter = await afterContext.Set<PupilTermResult>().AsNoTracking()
            .SingleAsync(p => p.ResultSetId == resultSetA, TestContext.Current.CancellationToken);
        aResultAfter.LevelPosition.ShouldBe(2); // now behind B's live 90
        aResultAfter.LevelPupilCount.ShouldBe(2);
        aResultAfter.Average.ShouldBe(80.00m); // A's OWN stored data is unaffected — only recomputed from A's own marks

        // B's stored computed row is untouched, byte-for-byte, by A's recompute.
        var bResultAfter = await afterContext.Set<PupilTermResult>().AsNoTracking()
            .SingleAsync(p => p.ResultSetId == resultSetB, TestContext.Current.CancellationToken);
        bResultAfter.Id.ShouldBe(bResultBaseline.Id);
        bResultAfter.TotalObtained.ShouldBe(bResultBaseline.TotalObtained);
        bResultAfter.Average.ShouldBe(bResultBaseline.Average);
        bResultAfter.LevelPosition.ShouldBe(bResultBaseline.LevelPosition);
        bResultAfter.LevelPositionTied.ShouldBe(bResultBaseline.LevelPositionTied);
        bResultAfter.LevelPupilCount.ShouldBe(bResultBaseline.LevelPupilCount);
        bResultAfter.ArmPosition.ShouldBe(bResultBaseline.ArmPosition);
    }

    // ---- Seeding and HTTP helpers ---------------------------------------------------------------

    private static object RowWithMarks(Guid pupilId, int? ca1, int? ca2, int? exam) => new
    {
        pupilId = pupilId.ToString(),
        componentMarks = new Dictionary<string, int?> { [Ca1Id.ToString()] = ca1, [Ca2Id.ToString()] = ca2 },
        examMark = exam,
        examAbsent = false,
    };

    private async Task<(Guid ArmId, Guid SubjectId, Guid TermId, Guid SessionId)> SeedArmWithSubjectAsync()
    {
        var (armId, subjectId, termId, levelId, sessionId) = await SeedArmWithSubjectFullAsync();
        return (armId, subjectId, termId, sessionId);
    }

    private async Task<(Guid ArmId, Guid SubjectId, Guid TermId, Guid LevelId, Guid SessionId)> SeedArmWithSubjectFullAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var levelId = (await context.ClassLevels.AsNoTracking().FirstAsync(TestContext.Current.CancellationToken)).Id;

        var startYear = Interlocked.Increment(ref _nextSessionStartYear);
        var session = AcademicSession.Create(
            Guid.CreateVersion7(), $"{startYear}/{startYear + 1}",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25)).Value;
        session.Activate();
        context.Add(session);

        var term = Term.Create(
            Guid.CreateVersion7(), session.Id, 1, "First Term",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear, 12, 12)).Value;
        term.Open().IsSuccess.ShouldBeTrue();
        context.Add(term);

        var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, null).Value;
        context.Add(arm);

        var subject = Subject.Create(Guid.CreateVersion7(), $"Subject {Guid.NewGuid():N}", null, null).Value;
        context.Add(subject);

        var mapping = SubjectMapping.Create(Guid.CreateVersion7(), subject.Id, levelId, session.Id, term.Id, 1).Value;
        context.Add(mapping);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (arm.Id, subject.Id, term.Id, levelId, session.Id);
    }

    private async Task<(Guid ArmId, Guid TermId)> SeedArmWithoutSubjectAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var levelId = (await context.ClassLevels.AsNoTracking().FirstAsync(TestContext.Current.CancellationToken)).Id;

        var startYear = Interlocked.Increment(ref _nextSessionStartYear);
        var session = AcademicSession.Create(
            Guid.CreateVersion7(), $"{startYear}/{startYear + 1}",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25)).Value;
        session.Activate();
        context.Add(session);

        var term = Term.Create(
            Guid.CreateVersion7(), session.Id, 1, "First Term",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear, 12, 12)).Value;
        term.Open().IsSuccess.ShouldBeTrue();
        context.Add(term);

        var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, null).Value;
        context.Add(arm);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (arm.Id, term.Id);
    }

    private async Task<Guid> SeedSiblingArmAsync(Guid levelId, Guid sessionId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var arm = Arm.Create(Guid.CreateVersion7(), levelId, sessionId, "B", null, null).Value;
        context.Add(arm);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return arm.Id;
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

    private async Task<Guid> SeedBareResultSetAsync(Guid armId, Guid termId, ResultSetState state)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var resultSet = ResultSet.Create(Guid.CreateVersion7(), armId, termId).Value;
        context.Add(resultSet);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (state != ResultSetState.Draft)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE result_set SET state = {state.ToString()} WHERE id = {resultSet.Id}",
                TestContext.Current.CancellationToken);
        }

        return resultSet.Id;
    }

    private async Task<Guid> GetResultSetIdAsync(Guid armId, Guid termId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.AsNoTracking()
            .SingleAsync(r => r.ArmId == armId && r.TermId == termId, TestContext.Current.CancellationToken);
        return resultSet.Id;
    }

    private async Task<CookieJar> SignInAsSuperAdminAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await SendAsyncCore(HttpMethod.Get, CsrfUrl, jar, jar.Apply);
        var signIn = await SendWithCsrfAsync(HttpMethod.Post, SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private Task<HttpResponseMessage> GetSheetAsync(Guid armId, Guid subjectId, Guid termId, CookieJar jar) =>
        SendAsyncCore(HttpMethod.Get, $"/api/v1/arms/{armId}/score-sheets?subjectId={subjectId}&termId={termId}", jar, jar.Apply);

    private Task<HttpResponseMessage> SaveSheetAsync(
        Guid armId, CookieJar jar, Guid subjectId, Guid termId, string? version, params object[] rows) =>
        SendWithCsrfAsync(
            HttpMethod.Put, $"/api/v1/arms/{armId}/score-sheets", jar,
            new { subjectId = subjectId.ToString(), termId = termId.ToString(), version, rows });

    private Task<HttpResponseMessage> ComputeAsync(Guid resultSetId, CookieJar jar) =>
        SendWithCsrfNoBodyAsync(HttpMethod.Post, $"/api/v1/result-sets/{resultSetId}/compute", jar);

    private async Task<HttpResponseMessage> SendAsyncCore(HttpMethod method, string url, CookieJar jar, Action<HttpRequestMessage> apply)
    {
        using var request = new HttpRequestMessage(method, url);
        apply(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync<T>(HttpMethod method, string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(payload) };
        jar.ApplyWithCsrf(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private async Task<HttpResponseMessage> SendWithCsrfNoBodyAsync(HttpMethod method, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(method, url);
        jar.ApplyWithCsrf(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
