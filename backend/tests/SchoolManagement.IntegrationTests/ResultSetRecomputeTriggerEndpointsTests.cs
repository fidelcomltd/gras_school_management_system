using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Domain.Subjects;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Configurations;
using SchoolManagement.Infrastructure.Persistence.Repositories;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0088 stage A (spec 6.2.9, 6.7.11): a §6.2.9 settings save and a
/// subject-mapping change both flag every non-Published result set they touch, in the SAME
/// transaction as their own write (AC A1/A2), and the new row-lock rule (AC A4) makes a settings
/// flag survive a genuinely concurrent compute rather than being silently clobbered by it — the
/// exact lost-update this card's carding found and no drift entry had named.
/// </summary>
public sealed class ResultSetRecomputeTriggerEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string GradingUrl = "/api/v1/settings/grading";
    private static readonly Guid Ca1Id = AssessmentComponentConfiguration.SeededIds[0];
    private static readonly Guid Ca2Id = AssessmentComponentConfiguration.SeededIds[1];
    private static int _nextSessionStartYear = 9000;

    // ---- AC A1: one settings save, end to end -------------------------------------------------

    [Fact]
    public async Task UpdateGrading_FlagsAnAlreadyComputedDraftResultSetInTheActiveSession()
    {
        RequireDatabase();
        var (armId, termId, _) = await SeedArmAsync();
        var resultSetId = await SeedBareResultSetAsync(armId, termId, ResultSetState.Draft, needsRecompute: false);
        var jar = await SignInAsSuperAdminAsync();

        var response = await PutAsync(GradingUrl, jar, TwoBandScale(expectedVersion: 0));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.AsNoTracking()
            .SingleAsync(r => r.Id == resultSetId, TestContext.Current.CancellationToken);
        resultSet.NeedsRecompute.ShouldBeTrue();
        resultSet.State.ShouldBe(ResultSetState.Draft); // Draft only gains the flag; it never moves.
    }

    // ---- AC A2: one subject-mapping change, end to end ----------------------------------------

    [Fact]
    public async Task SaveSubjectMappingGrid_AddingASubjectToTheLevel_FlagsTheLevelsAlreadyComputedDraftResultSet()
    {
        RequireDatabase();
        var (armId, subjectId, termId, levelId) = await SeedArmWithSubjectAsync();
        var resultSetId = await SeedBareResultSetAsync(armId, termId, ResultSetState.Draft, needsRecompute: false);
        var jar = await SignInAsSuperAdminAsync();

        var newSubjectId = await SeedActiveSubjectAsync();

        // The full desired grid: the existing mapping kept, plus a genuine addition — never an
        // ending, so the save never trips the marks-recorded guard.
        var command = new SaveSubjectMappingGridCommand(
            termId.ToString(),
            [
                new SubjectMappingGridEntryInput(subjectId.ToString(), levelId.ToString(), 1),
                new SubjectMappingGridEntryInput(newSubjectId.ToString(), levelId.ToString(), 2),
            ],
            DryRun: false);

        var response = await PutAsync($"/api/v1/subject-mappings?term_id={termId}", jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<SaveSubjectMappingGridResponse>(response);
        body.Additions.Count.ShouldBe(1);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.AsNoTracking()
            .SingleAsync(r => r.Id == resultSetId, TestContext.Current.CancellationToken);
        resultSet.NeedsRecompute.ShouldBeTrue();
        resultSet.State.ShouldBe(ResultSetState.Draft);
    }

    // ---- AC A4: real concurrency — a settings save races compute --------------------------------

    // The originally-approved delta's own decision paragraph names the exact bug this proves fixed:
    // "a settings flag committing while compute runs is overwritten by compute's NeedsRecompute =
    // false" — result_set carries no concurrency token, so a slow compute's own unconditional final
    // write can silently erase a flag a settings save set WHILE compute was still running.
    //
    // Review finding: firing both requests together and only checking the end state is NOT proof —
    // the ordinary serial interleaving (compute finishes completely, THEN settings flags) produces
    // the exact same "flag true at the end" outcome with no lock involved at all. To force the actual
    // race window, this test pauses compute, via a DI-replaceable dependency it already has
    // (IResultComputationRepository — no production code added), AFTER compute has taken its row
    // lock and read every input (arm, rules, subjects, roster, marks, components, bands) but BEFORE
    // it writes a single row or clears NeedsRecompute. With compute paused there, the settings save
    // is fired and PROVEN blocked (has not completed after a real wait), which is only possible if it
    // is waiting on the row lock compute is holding. Only then is compute released.
    [Fact]
    public async Task UpdateGrading_RacingCompute_EndsWithTheFlagSet()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedArmWithSubjectAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Okonkwo");

        var gate = new PauseGate();
        await using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IResultComputationRepository>();
            services.AddScoped<IResultComputationRepository>(sp =>
                new PausingResultComputationRepository(
                    new ResultComputationRepository(sp.GetRequiredService<ApplicationDbContext>()), gate));
        }));
        using var client = factory.CreateClient();

        var jar = await SignInAsSuperAdminAsync(client);
        await SaveSheetAsync(armId, jar, subjectId, termId, null, [RowWithMarks(pupilId, 18, 16, 52)], client); // complete: compute can run
        var resultSetId = await GetResultSetIdAsync(armId, termId);

        using var computeRequest = BuildPostRequestNoBody($"/api/v1/result-sets/{resultSetId}/compute", jar);
        var computeTask = client.SendAsync(computeRequest, TestContext.Current.CancellationToken);

        // Compute has taken its row lock, read everything, and computed in memory — it is now
        // paused immediately before its first write. If this never signals, compute never reached
        // that point (a real failure, not a flaky one) and the 10s wait times out loudly.
        await gate.Reached.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        using var settingsRequest = BuildPutRequest(GradingUrl, jar, TwoBandScale(expectedVersion: 0));
        var settingsTask = client.SendAsync(settingsRequest, TestContext.Current.CancellationToken);

        // Proof this is a genuine block, not a coincidence of timing: the settings save tries to
        // lock the SAME result_set row compute is still holding, so it must still be pending after a
        // real wait. Under the vacuous version of this test (no pause, no lock), this would already
        // have completed.
        await Task.Delay(TimeSpan.FromMilliseconds(750), TestContext.Current.CancellationToken);
        settingsTask.IsCompleted.ShouldBeFalse();

        gate.Release();

        var responses = await Task.WhenAll(computeTask, settingsTask);

        try
        {
            foreach (var response in responses)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            await using var scope = Fixture.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var resultSet = await context.ResultSets.AsNoTracking()
                .SingleAsync(r => r.Id == resultSetId, TestContext.Current.CancellationToken);

            resultSet.NeedsRecompute.ShouldBeTrue();
            resultSet.ComputedAtUtc.ShouldNotBeNull(); // compute genuinely ran, not merely rejected
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    /// <summary>
    /// Test-only synchronisation point for the concurrency test above. Not production code: it wraps
    /// the real <see cref="IResultComputationRepository"/> — an existing DI-replaceable dependency —
    /// so the test can observe "compute has read everything and is about to write" and hold it there
    /// deterministically, rather than hoping a race resolves a particular way.
    /// </summary>
    private sealed class PauseGate
    {
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Reached => _reached.Task;

        public void Release() => _release.TrySetResult();

        public async Task WaitHereAsync(CancellationToken cancellationToken)
        {
            _reached.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class PausingResultComputationRepository(IResultComputationRepository inner, PauseGate gate)
        : IResultComputationRepository
    {
        public async Task ReplaceComputedRowsAsync(
            Guid resultSetId,
            IReadOnlyList<SubjectResultLine> subjectLines,
            IReadOnlyList<SubjectArmStatistic> subjectStatistics,
            IReadOnlyList<PupilTermResult> pupilResults,
            CancellationToken cancellationToken)
        {
            await gate.WaitHereAsync(cancellationToken).ConfigureAwait(false);
            await inner.ReplaceComputedRowsAsync(resultSetId, subjectLines, subjectStatistics, pupilResults, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    // ---- Seeding and HTTP helpers ---------------------------------------------------------------

    private static UpdateGradingCommand TwoBandScale(int expectedVersion) => new(
        [
            new GradingBandInput(50, 100, "P", "Pass"),
            new GradingBandInput(0, 49, "F", "Fail"),
        ],
        expectedVersion,
        null);

    private static object RowWithMarks(Guid pupilId, int? ca1, int? ca2, int? exam) => new
    {
        pupilId = pupilId.ToString(),
        componentMarks = new Dictionary<string, int?> { [Ca1Id.ToString()] = ca1, [Ca2Id.ToString()] = ca2 },
        examMark = exam,
        examAbsent = false,
    };

    /// <summary>Seeds an active session, an open (Active) term, and one arm — no subject.</summary>
    private async Task<(Guid ArmId, Guid TermId, Guid LevelId)> SeedArmAsync()
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

        return (arm.Id, term.Id, levelId);
    }

    /// <summary>Seeds an active session, an open term, one arm, and a subject mapped to it.</summary>
    private async Task<(Guid ArmId, Guid SubjectId, Guid TermId, Guid LevelId)> SeedArmWithSubjectAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var (armId, termId, levelId) = await SeedArmAsync();

        var arm = await context.Arms.SingleAsync(a => a.Id == armId, TestContext.Current.CancellationToken);
        var subject = Subject.Create(Guid.CreateVersion7(), $"Subject {Guid.NewGuid():N}", null, null).Value;
        context.Add(subject);

        var mapping = SubjectMapping.Create(Guid.CreateVersion7(), subject.Id, levelId, arm.SessionId, termId, 1).Value;
        context.Add(mapping);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (armId, subject.Id, termId, levelId);
    }

    private async Task<Guid> SeedActiveSubjectAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var subject = Subject.Create(Guid.CreateVersion7(), $"Subject {Guid.NewGuid():N}", null, null).Value;
        context.Add(subject);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return subject.Id;
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

    private async Task<Guid> SeedBareResultSetAsync(Guid armId, Guid termId, ResultSetState state, bool needsRecompute)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var resultSet = ResultSet.Create(Guid.CreateVersion7(), armId, termId).Value;
        context.Add(resultSet);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (state != ResultSetState.Draft || !needsRecompute)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE result_set SET state = {state.ToString()}, needs_recompute = {needsRecompute} WHERE id = {resultSet.Id}",
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

    private Task<HttpResponseMessage> SaveSheetAsync(
        Guid armId, CookieJar jar, Guid subjectId, Guid termId, string? version, object[] rows, HttpClient? client = null) =>
        (client ?? Client).SendAsync(
            BuildPutRequest(
                $"/api/v1/arms/{armId}/score-sheets", jar,
                new { subjectId = subjectId.ToString(), termId = termId.ToString(), version, rows }),
            TestContext.Current.CancellationToken);

    private async Task<CookieJar> SignInAsSuperAdminAsync(HttpClient? client = null)
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar, client);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password), client);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private async Task<HttpResponseMessage> GetAsync(string url, CookieJar jar, HttpClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);
        return await SendCapturingAsync(request, jar, client).ConfigureAwait(false);
    }

    private Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload, HttpClient? client = null) =>
        SendCapturingAsync(BuildPostRequest(url, jar, payload), jar, client);

    private Task<HttpResponseMessage> PutAsync<T>(string url, CookieJar jar, T payload, HttpClient? client = null) =>
        SendCapturingAsync(BuildPutRequest(url, jar, payload), jar, client);

    private async Task<HttpResponseMessage> SendCapturingAsync(HttpRequestMessage request, CookieJar jar, HttpClient? client = null)
    {
        var response = await (client ?? Client).SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private static HttpRequestMessage BuildPostRequest<T>(string url, CookieJar jar, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        jar.ApplyWithCsrf(request);
        return request;
    }

    private static HttpRequestMessage BuildPostRequestNoBody(string url, CookieJar jar)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        jar.ApplyWithCsrf(request);
        return request;
    }

    private static HttpRequestMessage BuildPutRequest<T>(string url, CookieJar jar, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(payload) };
        jar.ApplyWithCsrf(request);
        return request;
    }
}
