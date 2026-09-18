using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Application.Abstractions.Audit;
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
/// End-to-end proof of TASK-0076 dispatch B's three score-sheet paths (spec 6.7.4): blank vs zero,
/// the first-save state transition, whole-sheet validation, staleness, the lock states, and void.
/// Signs in as a real seeded Super Admin throughout — every route's privilege is arm-scoped, and a
/// Super Admin holds every privilege school-wide, so this proves the endpoints without also having to
/// exercise the arm-scope substrate a different card already covers (<c>PrivilegeAuthorizationTests</c>).
/// </summary>
public sealed class ScoreSheetEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    // ---- GET ---------------------------------------------------------------------------------

    [Fact]
    public async Task Get_WithNoMarksEnteredAtAll_ReturnsEveryActivePupilBlank()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        await SeedPupilOnRosterAsync(armId, "Adeyemi");
        var jar = await SignInAsSuperAdminAsync();

        var response = await GetSheetAsync(armId, subjectId, termId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ScoreSheetDto>(response);
        body.Version.ShouldBeNull();
        body.ResultSet.ShouldBeNull();
        body.Rows.Count.ShouldBe(1);
        var row = body.Rows[0];
        row.ComponentMarks.Values.ShouldAllBe(mark => mark == null);
        row.ExamMark.ShouldBeNull();
        row.ExamAbsent.ShouldBeFalse();
        row.CaTotal.ShouldBeNull();
        row.SubjectTotal.ShouldBeNull();
    }

    // ---- Save: first-save transition, blank vs zero ------------------------------------------

    [Fact]
    public async Task Save_FirstSaveForTheArm_CreatesTheResultSetInDraftWithNeedsRecomputeTrue()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Bello");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(armId, jar, subjectId, termId, version: null, RowWithMarks(pupilId, ca1: 20, ca2: 18, exam: 55));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ScoreSheetDto>(response);
        body.ResultSet.ShouldNotBeNull();
        body.ResultSet!.State.ShouldBe(ResultSetState.Draft);
        body.ResultSet!.NeedsRecompute.ShouldBeTrue();
        body.Version.ShouldNotBeNull();
    }

    [Fact]
    public async Task Save_AStoredZero_ReadsBackAsZero_AndDiffersFromABlankCell()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        var zeroPupil = await SeedPupilOnRosterAsync(armId, "Chukwu");
        var blankPupil = await SeedPupilOnRosterAsync(armId, "Danladi");
        var jar = await SignInAsSuperAdminAsync();

        var save = await SaveSheetAsync(
            armId, jar, subjectId, termId, version: null,
            RowWithMarks(zeroPupil, ca1: 0, ca2: 0, exam: 0),
            RowWithMarks(blankPupil, ca1: null, ca2: null, exam: null));
        save.StatusCode.ShouldBe(HttpStatusCode.OK);

        var read = await GetSheetAsync(armId, subjectId, termId, jar);
        var body = await ReadAsync<ScoreSheetDto>(read);

        var zeroRow = body.Rows.Single(row => row.PupilId == zeroPupil.ToString());
        zeroRow.ComponentMarks.Values.ShouldAllBe(mark => mark == 0);
        zeroRow.ExamMark.ShouldBe(0);
        zeroRow.CaTotal.ShouldBe(0);
        zeroRow.SubjectTotal.ShouldBe(0);

        var blankRow = body.Rows.Single(row => row.PupilId == blankPupil.ToString());
        blankRow.ComponentMarks.Values.ShouldAllBe(mark => mark == null);
        blankRow.ExamMark.ShouldBeNull();
        blankRow.CaTotal.ShouldBeNull();
        blankRow.SubjectTotal.ShouldBeNull();
    }

    [Fact]
    public async Task Save_ARowGoingFullyBlankAndNotAbsent_DeletesTheExistingRow()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Emeka");
        var jar = await SignInAsSuperAdminAsync();

        var first = await SaveSheetAsync(armId, jar, subjectId, termId, version: null, RowWithMarks(pupilId, 15, 15, 40));
        var firstBody = await ReadAsync<ScoreSheetDto>(first);

        var second = await SaveSheetAsync(
            armId, jar, subjectId, termId, firstBody.Version, RowWithMarks(pupilId, null, null, null));

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondBody = await ReadAsync<ScoreSheetDto>(second);
        secondBody.Version.ShouldBeNull();
        secondBody.Rows.Single().ComponentMarks.Values.ShouldAllBe(mark => mark == null);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.SubjectScores.AsNoTracking().AnyAsync(
            score => score.PupilId == pupilId, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Save_WithAStaleVersion_Returns409()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Femi");
        var jar = await SignInAsSuperAdminAsync();

        await SaveSheetAsync(armId, jar, subjectId, termId, version: null, RowWithMarks(pupilId, 10, 10, 30));

        var retry = await SaveSheetAsync(armId, jar, subjectId, termId, version: null, RowWithMarks(pupilId, 12, 12, 30));

        retry.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(retry);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("score_sheet.stale_version");
    }

    [Fact]
    public async Task Save_WhileTheTermIsClosed_Returns409()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Grace");
        await CloseTermAsync(termId);
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(armId, jar, subjectId, termId, version: null, RowWithMarks(pupilId, 10, 10, 30));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("score_sheet.term_closed");
    }

    [Fact]
    public async Task Save_OnceTheResultSetIsApproved_Returns409Locked()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Halima");
        var jar = await SignInAsSuperAdminAsync();
        await SaveSheetAsync(armId, jar, subjectId, termId, version: null, RowWithMarks(pupilId, 10, 10, 30));
        await SetResultSetStateAsync(armId, termId, ResultSetState.Approved);

        var response = await SaveSheetAsync(armId, jar, subjectId, termId, version: null, RowWithMarks(pupilId, 12, 12, 30));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("score_sheet.result_set_locked");
    }

    // ---- Save: per-cell validation -------------------------------------------------------------

    [Fact]
    public async Task Save_MissingARequiredComponentKey_Returns422ComponentMissing()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Ibrahim");
        var jar = await SignInAsSuperAdminAsync();

        var response = await Client.SendAsync(
            BuildPutRequest(armId, jar, new
            {
                subjectId = subjectId.ToString(),
                termId = termId.ToString(),
                version = (string?)null,
                rows = new[]
                {
                    new
                    {
                        pupilId = pupilId.ToString(),
                        componentMarks = new Dictionary<string, int?> { [AssessmentComponentConfiguration.SeededIds[0].ToString()] = 10 },
                        examMark = (int?)30,
                        examAbsent = false,
                    },
                },
            }),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        var errors = document.RootElement.GetProperty("errors");
        errors.EnumerateObject().ShouldContain(property =>
            property.Value.EnumerateArray().Any(message => message.GetString()!.Contains("is required for every row")));
    }

    [Fact]
    public async Task Save_AMarkAboveTheComponentMaximum_Returns422NamingTheMaximum()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Josephine");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(armId, jar, subjectId, termId, version: null, RowWithMarks(pupilId, ca1: 21, ca2: 10, exam: 30));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        var errors = document.RootElement.GetProperty("errors");
        errors.EnumerateObject().ShouldContain(property =>
            property.Value.EnumerateArray().Any(message => message.GetString() == "Maximum for CA1 is 20."));
    }

    [Fact]
    public async Task Save_AbsentWithAnExamMarkAlsoSupplied_Returns422ExamConflict()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Kelechi");
        var jar = await SignInAsSuperAdminAsync();

        var response = await Client.SendAsync(
            BuildPutRequest(armId, jar, new
            {
                subjectId = subjectId.ToString(),
                termId = termId.ToString(),
                version = (string?)null,
                rows = new[]
                {
                    new
                    {
                        pupilId = pupilId.ToString(),
                        componentMarks = ComponentDictionary(10, 10),
                        examMark = (int?)40,
                        examAbsent = true,
                    },
                },
            }),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").ToString().ShouldContain("absent and given an exam mark");
    }

    [Fact]
    public async Task Save_APupilNotOnTheRoster_Returns422()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        await SeedPupilOnRosterAsync(armId, "Latifah");
        var jar = await SignInAsSuperAdminAsync();

        var response = await SaveSheetAsync(armId, jar, subjectId, termId, version: null, RowWithMarks(Guid.CreateVersion7(), 10, 10, 30));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errors").ToString().ShouldContain("not on the arm's active roster");
    }

    // ---- Audit -------------------------------------------------------------------------------

    [Fact]
    public async Task Save_PopulatesBothBeforeAndAfterJsonOnTheAuditEvent()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Musa");

        var fakeAuditSink = new RecordingSystemAuditSink2();
        await using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemAuditSink>();
            services.AddSingleton<ISystemAuditSink>(fakeAuditSink);
        }));
        using var client = factory.CreateClient();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await SendAsyncCore(client, HttpMethod.Get, CsrfUrl, jar, jar.Apply);
        var signIn = await SendWithCsrfAsync(client, HttpMethod.Post, SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await SendWithCsrfAsync(
            client, HttpMethod.Put, $"/api/v1/arms/{armId}/score-sheets", jar,
            new { subjectId = subjectId.ToString(), termId = termId.ToString(), version = (string?)null, rows = new[] { RowWithMarks(pupilId, 10, 10, 30) } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The fake sink also observes background housekeeping (e.g. system.idempotency_purge) that
        // has nothing to do with this save, so pick out this handler's own record rather than
        // assume it is the only one recorded during the request.
        var record = fakeAuditSink.Records.Single(r => r.Action == "result.score.enter" && r.EntityType == "subject_score");
        record.Metadata.ShouldNotBeNull();
        record.BeforeMetadata.ShouldNotBeNull();
    }

    // ---- Void --------------------------------------------------------------------------------

    [Fact]
    public async Task Void_StampsEveryActiveRow_RemovesThemFromGet_AndSetsNeedsRecompute()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        var pupilId = await SeedPupilOnRosterAsync(armId, "Ngozi");
        var jar = await SignInAsSuperAdminAsync();
        await SaveSheetAsync(armId, jar, subjectId, termId, version: null, RowWithMarks(pupilId, 10, 10, 30));
        await SetResultSetNeedsRecomputeAsync(armId, termId, false);

        var voidResponse = await Client.SendAsync(
            BuildPostRequest($"/api/v1/arms/{armId}/score-sheets/void", jar, new
            {
                subjectId = subjectId.ToString(),
                termId = termId.ToString(),
                reason = "Whole class re-marked after a transcription error in the mark book.",
            }),
            TestContext.Current.CancellationToken);

        voidResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var voidBody = await ReadAsync<VoidScoreSheetResponse>(voidResponse);
        voidBody.VoidedCount.ShouldBe(1);

        var read = await GetSheetAsync(armId, subjectId, termId, jar);
        var readBody = await ReadAsync<ScoreSheetDto>(read);
        readBody.Version.ShouldBeNull();
        readBody.Rows.Single().ComponentMarks.Values.ShouldAllBe(mark => mark == null);
        readBody.ResultSet!.NeedsRecompute.ShouldBeTrue();

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var score = await context.SubjectScores.AsNoTracking()
            .SingleAsync(s => s.PupilId == pupilId, TestContext.Current.CancellationToken);
        score.VoidedAt.ShouldNotBeNull();
        score.VoidReason.ShouldBe("Whole class re-marked after a transcription error in the mark book.");
    }

    [Fact]
    public async Task Void_WithAReasonUnderTenCharacters_Returns422()
    {
        RequireDatabase();
        var (armId, subjectId, termId, _) = await SeedSheetAsync();
        var jar = await SignInAsSuperAdminAsync();

        var response = await Client.SendAsync(
            BuildPostRequest($"/api/v1/arms/{armId}/score-sheets/void", jar, new
            {
                subjectId = subjectId.ToString(),
                termId = termId.ToString(),
                reason = "too short",
            }),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // ---- Seeding and HTTP helpers ----------------------------------------------------------------

    private static readonly Guid Ca1Id = AssessmentComponentConfiguration.SeededIds[0];
    private static readonly Guid Ca2Id = AssessmentComponentConfiguration.SeededIds[1];
    private static int _nextSessionStartYear = 4000;

    private static object RowWithMarks(Guid pupilId, int? ca1, int? ca2, int? exam) => new
    {
        pupilId = pupilId.ToString(),
        componentMarks = ComponentDictionary(ca1, ca2),
        examMark = exam,
        examAbsent = false,
    };

    private static Dictionary<string, int?> ComponentDictionary(int? ca1, int? ca2) => new()
    {
        [Ca1Id.ToString()] = ca1,
        [Ca2Id.ToString()] = ca2,
    };

    /// <summary>Seeds an active session, an open (Active) term, one arm, and a subject mapped to it.</summary>
    private async Task<(Guid ArmId, Guid SubjectId, Guid TermId, Guid SessionId)> SeedSheetAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var levelId = (await context.ClassLevels.AsNoTracking().FirstAsync(TestContext.Current.CancellationToken)).Id;

        var startYear = Interlocked.Increment(ref _nextSessionStartYear);
        var session = SchoolManagement.Domain.Sessions.AcademicSession.Create(
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

        var mapping = SchoolManagement.Domain.Subjects.SubjectMapping.Create(
            Guid.CreateVersion7(), subject.Id, levelId, session.Id, term.Id, 1).Value;
        context.Add(mapping);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (arm.Id, subject.Id, term.Id, session.Id);
    }

    private async Task<Guid> SeedPupilOnRosterAsync(Guid armId, string surname)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Every call site in this file uses a distinct literal surname and each test resets the
        // database first, so no de-duplicating suffix is needed — unlike EnrolmentPersistenceTests'
        // SeedPupilAsync, which seeds many pupils per test run.
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

        // Pupil.Create starts a pupil at PupilStatus.Pending (admission not yet approved); the
        // roster query requires Active, and there is no ordinary write path to it — same accepted
        // direct-SQL technique EnrolmentPersistenceTests.SeedPupilAsync uses.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET status = {nameof(PupilStatus.Active)} WHERE id = {pupil.Id}",
            TestContext.Current.CancellationToken);
        return pupil.Id;
    }

    private async Task CloseTermAsync(Guid termId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var term = await context.Terms.SingleAsync(t => t.Id == termId, TestContext.Current.CancellationToken);
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        // Close() requires TimesSchoolOpened to be set first (spec 6.3.4/6.3.6) — Term.Create leaves
        // it blank, so setting it here is what makes this a genuine close rather than a no-op failure.
        term.SetTimesSchoolOpened(60).IsSuccess.ShouldBeTrue();
        term.Close(timeProvider.GetUtcNow(), null).IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
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

    private async Task SetResultSetNeedsRecomputeAsync(Guid armId, Guid termId, bool value)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.SingleAsync(
            r => r.ArmId == armId && r.TermId == termId, TestContext.Current.CancellationToken);
        typeof(ResultSet).GetProperty(nameof(ResultSet.NeedsRecompute))!.SetValue(resultSet, value);
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

    private Task<HttpResponseMessage> GetSheetAsync(Guid armId, Guid subjectId, Guid termId, CookieJar jar) =>
        GetAsyncCore($"/api/v1/arms/{armId}/score-sheets?subjectId={subjectId}&termId={termId}", jar);

    private Task<HttpResponseMessage> SaveSheetAsync(
        Guid armId, CookieJar jar, Guid subjectId, Guid termId, string? version, params object[] rows) =>
        Client.SendAsync(
            BuildPutRequest(armId, jar, new { subjectId = subjectId.ToString(), termId = termId.ToString(), version, rows }),
            TestContext.Current.CancellationToken);

    private static HttpRequestMessage BuildPutRequest<T>(Guid armId, CookieJar jar, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/arms/{armId}/score-sheets")
        {
            Content = JsonContent.Create(payload),
        };
        jar.ApplyWithCsrf(request);
        return request;
    }

    private static HttpRequestMessage BuildPostRequest<T>(string url, CookieJar jar, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
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

/// <summary>Records every audit call, including the before/after metadata dictionaries this card adds.</summary>
internal sealed class RecordingSystemAuditSink2 : ISystemAuditSink
{
    public List<(string Action, string? EntityType, string? EntityId, IReadOnlyDictionary<string, object?>? Metadata,
        IReadOnlyDictionary<string, object?>? BeforeMetadata)> Records
    { get; } = [];

    public Task RecordAsync(
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        string? actorAdminId,
        CancellationToken cancellationToken,
        string? reason = null,
        IReadOnlyDictionary<string, object?>? beforeMetadata = null)
    {
        Records.Add((action, entityType, entityId, metadata, beforeMetadata));
        return Task.CompletedTask;
    }

    public Task RecordRejectionAsync(
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        string? actorAdminId,
        CancellationToken cancellationToken,
        string? reason = null) => Task.CompletedTask;
}
