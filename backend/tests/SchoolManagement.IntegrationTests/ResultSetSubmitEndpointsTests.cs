using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Subjects;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Configurations;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0088 stage B's submit endpoint (spec §6.7.5, §6.7.11): the happy path,
/// the 422 whose <c>readiness</c> body matches the GET (AC B6), the 403/409 refusals the contract
/// delta names, that every sheet save except the head teacher's remark 409s once submitted (AC B5),
/// and the deterministic proof that a concurrent score save either lands before submit commits or is
/// refused 409 (AC A4, stage A's deferred test).
/// </summary>
public sealed class ResultSetSubmitEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    private static readonly Guid Ca1Id = AssessmentComponentConfiguration.SeededIds[0];
    private static readonly Guid Ca2Id = AssessmentComponentConfiguration.SeededIds[1];
    private static readonly Guid PrimaryExcellentPointId = RatingScalePointConfiguration.AllSeededIds[6];

    private static int _nextSessionStartYear = 9700;

    // ---- Happy path ------------------------------------------------------------------------------

    [Fact]
    public async Task Submit_AReadyDraftSet_MovesToAwaitingApprovalAndReturns200()
    {
        RequireDatabase();
        var (armId, termId, resultSetId, _, jar) = await SeedReadySetAsync();

        var response = await SubmitAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<SubmitResultSetResponse>(response);
        body.ResultSet.State.ShouldBe(ResultSetState.AwaitingApproval);
        body.ResultSet.NeedsRecompute.ShouldBeFalse();

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.AsNoTracking()
            .SingleAsync(r => r.Id == resultSetId, TestContext.Current.CancellationToken);
        resultSet.State.ShouldBe(ResultSetState.AwaitingApproval);
        resultSet.SubmittedAtUtc.ShouldNotBeNull();
        resultSet.SubmittedBy.ShouldNotBeNull();

        _ = armId;
        _ = termId;
    }

    [Fact]
    public async Task Submit_AReturnedForCorrectionSet_ResubmitsAndClearsTheReturnReason()
    {
        RequireDatabase();
        var (_, _, resultSetId, _, jar) = await SeedReadySetAsync();
        await SetResultSetStateAndReturnReasonAsync(resultSetId, ResultSetState.ReturnedForCorrection, "Please recheck Mathematics.");

        var response = await SubmitAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.AsNoTracking()
            .SingleAsync(r => r.Id == resultSetId, TestContext.Current.CancellationToken);
        resultSet.State.ShouldBe(ResultSetState.AwaitingApproval);
        resultSet.ReturnReason.ShouldBeNull();
    }

    // ---- AC B6: the 422 body equals the GET's readiness body for the same set ---------------------

    [Fact]
    public async Task Submit_ANotReadySet_Returns422WhoseReadinessBodyMatchesTheGetEndpoint()
    {
        RequireDatabase();
        var (armId, termId, resultSetId, _, jar) = await SeedReadySetAsync(skipTraitRatings: true);

        var getResponse = await Client.SendAsync(
            BuildGetRequest($"/api/v1/arms/{armId}/readiness?termId={termId}", jar), TestContext.Current.CancellationToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var getBody = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var submitResponse = await SubmitAsync(resultSetId, jar);

        submitResponse.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var problem = await ReadJsonAsync(submitResponse);
        problem.RootElement.GetProperty("errorCode").GetString().ShouldBe("result_set.not_ready");
        var readinessFromSubmit = problem.RootElement.GetProperty("readiness").GetRawText();

        using var getDocument = System.Text.Json.JsonDocument.Parse(getBody);
        var readinessFromGet = getDocument.RootElement.GetRawText();

        // Structural equality via re-serialisation, not raw string equality — property ORDER is not
        // part of the contract, only the VALUES are.
        System.Text.Json.JsonSerializer.Serialize(System.Text.Json.JsonDocument.Parse(readinessFromSubmit).RootElement)
            .ShouldBe(System.Text.Json.JsonSerializer.Serialize(System.Text.Json.JsonDocument.Parse(readinessFromGet).RootElement));
    }

    // ---- Refusals the contract delta names ---------------------------------------------------------

    [Fact]
    public async Task Submit_AnUnknownResultSetId_Returns403NotFound()
    {
        RequireDatabase();
        var jar = await SignInAsSuperAdminAsync();

        var response = await SubmitAsync(Guid.CreateVersion7(), jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Submit_AnAlreadyAwaitingApprovalSet_Returns409()
    {
        RequireDatabase();
        var (_, _, resultSetId, _, jar) = await SeedReadySetAsync();
        await SetResultSetStateAndReturnReasonAsync(resultSetId, ResultSetState.AwaitingApproval, null);

        var response = await SubmitAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("result_set.invalid_state");
    }

    [Fact]
    public async Task Submit_WithTermClosed_Returns409()
    {
        RequireDatabase();
        var (armId, termId, resultSetId, _, jar) = await SeedReadySetAsync();

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var term = await context.Terms.SingleAsync(t => t.Id == termId, TestContext.Current.CancellationToken);
            typeof(Term).GetProperty(nameof(Term.State))!.SetValue(term, TermState.Closed);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await SubmitAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("result_set.term_closed");

        _ = armId;
    }

    // ---- AC B5: once submitted, every sheet save except the head teacher's remark 409s -------------

    [Fact]
    public async Task Submit_ThenEverySheetSaveExceptTheHeadTeacherRemark_Returns409()
    {
        RequireDatabase();
        var (armId, termId, resultSetId, subjectId, jar) = await SeedReadySetAsync();
        var pupilId = await GetOnlyRosterPupilIdAsync(armId);

        var submitResponse = await SubmitAsync(resultSetId, jar);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var scoreResponse = await Client.SendAsync(
            BuildPutRequest(
                $"/api/v1/arms/{armId}/score-sheets", jar,
                new { subjectId = subjectId.ToString(), termId = termId.ToString(), version = (string?)null, rows = new[] { ScoreRow(pupilId, 10, 10, 10) } }),
            TestContext.Current.CancellationToken);
        scoreResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var traitResponse = await Client.SendAsync(
            BuildPutRequest(
                $"/api/v1/arms/{armId}/trait-ratings", jar,
                new { termId = termId.ToString(), version = (string?)null, rows = new[] { new { pupilId = pupilId.ToString(), ratings = new Dictionary<string, string?>() } } }),
            TestContext.Current.CancellationToken);
        traitResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var attendanceResponse = await Client.SendAsync(
            BuildPutRequest(
                $"/api/v1/arms/{armId}/attendance", jar,
                new { termId = termId.ToString(), version = (string?)null, rows = new[] { new { pupilId = pupilId.ToString(), timesPresent = 59 } } }),
            TestContext.Current.CancellationToken);
        attendanceResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var remarkResponse = await Client.SendAsync(
            BuildPutRequest(
                $"/api/v1/arms/{armId}/class-teacher-remarks", jar,
                new { termId = termId.ToString(), version = (string?)null, rows = new[] { new { pupilId = pupilId.ToString(), remark = "Edited after submit." } } }),
            TestContext.Current.CancellationToken);
        remarkResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // The head teacher's remark is deliberately NEVER locked by submission (TASK-0088 human
        // ruling: it gates publication, not submission) — proven by its OWN 200, not merely absence
        // of a 409 above.
        var headRemarkResponse = await Client.SendAsync(
            BuildPutRequest(
                $"/api/v1/arms/{armId}/head-teacher-remarks", jar,
                new { termId = termId.ToString(), version = (string?)null, rows = new[] { new { pupilId = pupilId.ToString(), remark = "Well done." } }, fillEmpty = (string?)null }),
            TestContext.Current.CancellationToken);
        headRemarkResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---- AC A4 (stage A's deferred test) / B5: a score save races submit ---------------------------

    // Review lesson carried over from stage A's own race test: firing both requests together and only
    // checking the end state is not proof, because the ordinary serial interleaving (submit finishes
    // completely, THEN the score save is refused) produces the exact same end state with no lock
    // involved. To force the real race window this pauses SUBMIT, via a DI-replaceable dependency it
    // already has (IResultSetReadinessEvaluator — no production code added FOR the test; the interface
    // exists so GET and POST share one gate implementation, see its own remarks), AFTER submit has
    // taken its row lock and checked state but BEFORE it writes. With submit paused there, the score
    // save is fired and PROVEN blocked (still pending after a real wait), which is only possible if it
    // is waiting on the row lock submit is holding. Only then is submit released.
    [Fact]
    public async Task Submit_RacingAScoreSave_TheScoreSaveEitherLandsFirstOrIsRefused409()
    {
        RequireDatabase();

        var gate = new PauseGate();
        await using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IResultSetReadinessEvaluator>();
            services.AddScoped<IResultSetReadinessEvaluator>(sp =>
                new PausingResultSetReadinessEvaluator(
                    new ResultSetReadinessEvaluator(
                        sp.GetRequiredService<Application.Abstractions.Classes.IClassLevelRepository>(),
                        sp.GetRequiredService<Application.Abstractions.Classes.ISectionRepository>(),
                        sp.GetRequiredService<Application.Abstractions.Enrolments.IEnrolmentRepository>(),
                        sp.GetRequiredService<Application.Subjects.SubjectsInEffectResolver>(),
                        sp.GetRequiredService<Application.Settings.IAssessmentComponentRepository>(),
                        sp.GetRequiredService<ISubjectScoreRepository>(),
                        sp.GetRequiredService<Application.Settings.ITraitRepository>(),
                        sp.GetRequiredService<ITraitRatingRepository>(),
                        sp.GetRequiredService<Application.Settings.IDevelopmentDomainRepository>(),
                        sp.GetRequiredService<IDevelopmentRatingRepository>(),
                        sp.GetRequiredService<IAttendanceEntryRepository>(),
                        sp.GetRequiredService<IPupilRemarkRepository>(),
                        sp.GetRequiredService<Application.Abstractions.Auth.IAdminAccountRepository>()),
                    gate));
        }));
        using var client = factory.CreateClient();

        var (armId, termId, resultSetId, subjectId, jar) = await SeedReadySetAsync(client: client);
        var pupilId = await GetOnlyRosterPupilIdAsync(armId);

        using var submitRequest = BuildPostRequestNoBody($"/api/v1/result-sets/{resultSetId}/submit", jar);
        var submitTask = client.SendAsync(submitRequest, TestContext.Current.CancellationToken);

        // Submit has taken its row lock, checked state, and is inside readiness evaluation — paused
        // immediately before it can write. If this never signals, submit never reached that point (a
        // real failure, not a flaky one) and the 10s wait times out loudly.
        await gate.Reached.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        using var scoreSaveRequest = BuildPutRequest(
            $"/api/v1/arms/{armId}/score-sheets", jar,
            new { subjectId = subjectId.ToString(), termId = termId.ToString(), version = (string?)null, rows = new[] { ScoreRow(pupilId, 12, 12, 12) } });
        var scoreSaveTask = client.SendAsync(scoreSaveRequest, TestContext.Current.CancellationToken);

        // Proof this is a genuine block, not a coincidence of timing: the score save tries to lock the
        // SAME result_set row submit is still holding, so it must still be pending after a real wait.
        // Under the vacuous version of this test (submit's lock finder swapped for the non-locking
        // one), this would already have completed — see this dispatch's report for the RED/GREEN run.
        await Task.Delay(TimeSpan.FromMilliseconds(750), TestContext.Current.CancellationToken);
        scoreSaveTask.IsCompleted.ShouldBeFalse();

        gate.Release();

        var responses = await Task.WhenAll(submitTask, scoreSaveTask);

        try
        {
            responses[0].StatusCode.ShouldBe(HttpStatusCode.OK); // submit always succeeds: it took the lock first.

            // The score save either landed before submit committed (impossible here, since submit was
            // paused AFTER checking state and BEFORE writing — so by construction it can only be
            // refused) or is refused 409 once it sees Awaiting Approval. Both are the contract's
            // "either lands before the submit or is refused with 409" — this test always exercises the
            // refused branch by construction; the seeded arm has exactly one active pupil and subject,
            // so no OTHER caller can slip in between.
            responses[1].StatusCode.ShouldBe(HttpStatusCode.Conflict);

            await using var scope = Fixture.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var resultSet = await context.ResultSets.AsNoTracking()
                .SingleAsync(r => r.Id == resultSetId, TestContext.Current.CancellationToken);
            resultSet.State.ShouldBe(ResultSetState.AwaitingApproval); // submit genuinely committed, not merely accepted
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
    /// Test-only synchronisation point, same shape as stage A's own <c>PauseGate</c>
    /// (<c>ResultSetRecomputeTriggerEndpointsTests</c>) — not production code.
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

    private sealed class PausingResultSetReadinessEvaluator(IResultSetReadinessEvaluator inner, PauseGate gate)
        : IResultSetReadinessEvaluator
    {
        public async Task<ResultSetReadinessDto> EvaluateAsync(
            Arm arm, Term term, ResultSet? resultSet, CancellationToken cancellationToken)
        {
            await gate.WaitHereAsync(cancellationToken).ConfigureAwait(false);
            return await inner.EvaluateAsync(arm, term, resultSet, cancellationToken).ConfigureAwait(false);
        }
    }

    // ---- Seeding and HTTP helpers -------------------------------------------------------------------

    private static object ScoreRow(Guid pupilId, int? ca1, int? ca2, int? exam) => new
    {
        pupilId = pupilId.ToString(),
        componentMarks = new Dictionary<string, int?> { [Ca1Id.ToString()] = ca1, [Ca2Id.ToString()] = ca2 },
        examMark = exam,
        examAbsent = false,
    };

    /// <summary>
    /// A Draft, computed, gate-passing result set: one primary arm, one subject, one pupil with
    /// complete marks/trait ratings/attendance/class-teacher remark, an active form teacher, and the
    /// term's <c>timesSchoolOpened</c> set.
    /// </summary>
    private async Task<(Guid ArmId, Guid TermId, Guid ResultSetId, Guid SubjectId, CookieJar Jar)> SeedReadySetAsync(
        bool skipTraitRatings = false, HttpClient? client = null)
    {
        var (accountId, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false, email: $"admin-{Guid.NewGuid():N}@example.com");

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
        term.SetTimesSchoolOpened(60);
        context.Add(term);

        var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, accountId).Value;
        context.Add(arm);

        var subject = Subject.Create(Guid.CreateVersion7(), $"Subject {Guid.NewGuid():N}", null, null).Value;
        context.Add(subject);
        var mapping = SubjectMapping.Create(Guid.CreateVersion7(), subject.Id, levelId, session.Id, term.Id, 1).Value;
        context.Add(mapping);

        var pupil = Pupil.Create(
            Guid.CreateVersion7(), "Okafor", "Chidera", middleName: null, PupilSex.Female,
            new DateOnly(2018, 1, 1), asOfDate: new DateOnly(2026, 9, 9), nationality: null,
            "Anambra", "Awka South", "14 Zik Avenue, Awka",
            previousSchool: null, previousClass: null, otherInformation: null).Value;
        context.Add(pupil);
        var enrolment = Enrolment.Open(Guid.CreateVersion7(), pupil.Id, arm.Id, new DateOnly(2026, 9, 14)).Value;
        context.Add(enrolment);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET status = {nameof(PupilStatus.Active)} WHERE id = {pupil.Id}",
            TestContext.Current.CancellationToken);

        var jar = new CookieJar();
        var httpClient = client ?? Client;
        await GetAsyncCore(CsrfUrl, jar, httpClient);
        var signIn = await PostAsyncCore(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password), httpClient);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var scoreResponse = await httpClient.SendAsync(
            BuildPutRequest(
                $"/api/v1/arms/{arm.Id}/score-sheets", jar,
                new { subjectId = subject.Id.ToString(), termId = term.Id.ToString(), version = (string?)null, rows = new[] { ScoreRow(pupil.Id, 15, 15, 55) } }),
            TestContext.Current.CancellationToken);
        scoreResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        if (!skipTraitRatings)
        {
            var ratings = TraitConfiguration.AllSeededIds.ToDictionary(traitId => traitId.ToString(), _ => (string?)PrimaryExcellentPointId.ToString());
            var traitResponse = await httpClient.SendAsync(
                BuildPutRequest(
                    $"/api/v1/arms/{arm.Id}/trait-ratings", jar,
                    new { termId = term.Id.ToString(), version = (string?)null, rows = new[] { new { pupilId = pupil.Id.ToString(), ratings } } }),
                TestContext.Current.CancellationToken);
            traitResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var attendanceResponse = await httpClient.SendAsync(
            BuildPutRequest(
                $"/api/v1/arms/{arm.Id}/attendance", jar,
                new { termId = term.Id.ToString(), version = (string?)null, rows = new[] { new { pupilId = pupil.Id.ToString(), timesPresent = 58 } } }),
            TestContext.Current.CancellationToken);
        attendanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var remarkResponse = await httpClient.SendAsync(
            BuildPutRequest(
                $"/api/v1/arms/{arm.Id}/class-teacher-remarks", jar,
                new { termId = term.Id.ToString(), version = (string?)null, rows = new[] { new { pupilId = pupil.Id.ToString(), remark = "A pleasure to teach." } } }),
            TestContext.Current.CancellationToken);
        remarkResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var resultSetId = await GetResultSetIdAsync(arm.Id, term.Id);

        var computeResponse = await httpClient.SendAsync(
            BuildPostRequestNoBody($"/api/v1/result-sets/{resultSetId}/compute", jar), TestContext.Current.CancellationToken);
        computeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (arm.Id, term.Id, resultSetId, subject.Id, jar);
    }

    private async Task<Guid> GetResultSetIdAsync(Guid armId, Guid termId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.AsNoTracking()
            .SingleAsync(r => r.ArmId == armId && r.TermId == termId, TestContext.Current.CancellationToken);
        return resultSet.Id;
    }

    private async Task<Guid> GetOnlyRosterPupilIdAsync(Guid armId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var enrolment = await context.Enrolments.AsNoTracking()
            .SingleAsync(e => e.ArmId == armId && e.EffectiveTo == null, TestContext.Current.CancellationToken);
        return enrolment.PupilId;
    }

    private async Task SetResultSetStateAndReturnReasonAsync(Guid resultSetId, ResultSetState state, string? returnReason)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.SingleAsync(r => r.Id == resultSetId, TestContext.Current.CancellationToken);
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, state);
        typeof(ResultSet).GetProperty(nameof(ResultSet.ReturnReason))!.SetValue(resultSet, returnReason);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<CookieJar> SignInAsSuperAdminAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsyncCore(CsrfUrl, jar, Client);
        var signIn = await PostAsyncCore(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password), Client);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private Task<HttpResponseMessage> SubmitAsync(Guid resultSetId, CookieJar jar) =>
        Client.SendAsync(BuildPostRequestNoBody($"/api/v1/result-sets/{resultSetId}/submit", jar), TestContext.Current.CancellationToken);

    private static HttpRequestMessage BuildGetRequest(string url, CookieJar jar)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);
        return request;
    }

    private static HttpRequestMessage BuildPutRequest<T>(string url, CookieJar jar, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(payload) };
        jar.ApplyWithCsrf(request);
        return request;
    }

    private static HttpRequestMessage BuildPostRequestNoBody(string url, CookieJar jar)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        jar.ApplyWithCsrf(request);
        return request;
    }

    private static Task<HttpResponseMessage> GetAsyncCore(string url, CookieJar jar, HttpClient client) => SendAsyncCore(client, HttpMethod.Get, url, jar, jar.Apply);

    private static Task<HttpResponseMessage> PostAsyncCore<T>(string url, CookieJar jar, T payload, HttpClient client) =>
        SendWithCsrfAsync(client, HttpMethod.Post, url, jar, payload);

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
