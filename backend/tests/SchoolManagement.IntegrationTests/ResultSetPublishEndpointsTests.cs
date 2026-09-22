using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Spec 6.7.9 publication end to end: the happy path writes the snapshot, and each precondition fails
/// with its own code. Seeds a bare result set in the target state, as the approval tests do. The arm has
/// no pupils, so the head-teacher-remark check passes trivially here; its counting is TASK-0088's
/// readiness evaluator, covered there.
/// </summary>
public sealed class ResultSetPublishEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    private static int _nextSessionStartYear = 9700;

    [Fact]
    public async Task Publish_AnApprovedReadySet_PublishesAndWritesTheSnapshot()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedAsync();

        var response = await PublishAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<PublishResultSetResponse>(response);
        body.ResultSet.State.ShouldBe(ResultSetState.Published);
        body.RevisionNumber.ShouldBe(1);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.AsNoTracking().SingleAsync(r => r.Id == resultSetId, TestContext.Current.CancellationToken);
        resultSet.State.ShouldBe(ResultSetState.Published);
        resultSet.PublishedAtUtc.ShouldNotBeNull();
        resultSet.RevisionNumber.ShouldBe(1);

        using var snapshot = JsonDocument.Parse(resultSet.ConfigSnapshotJson!);
        snapshot.RootElement.GetProperty("settings").GetProperty("identity").ValueKind.ShouldBe(JsonValueKind.Object);
        snapshot.RootElement.GetProperty("arm").GetProperty("displayName").GetString().ShouldNotBeNullOrWhiteSpace();
        snapshot.RootElement.GetProperty("logoUploadGroupId").GetGuid().ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Publish_ASetThatIsNotApproved_Returns409NotApproved()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedAsync(state: ResultSetState.AwaitingApproval);

        await ShouldFailAsync(await PublishAsync(resultSetId, jar), "result_set.not_approved");
    }

    [Fact]
    public async Task Publish_WhenNeedsRecomputeIsSet_Returns409()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedAsync(needsRecompute: true);

        await ShouldFailAsync(await PublishAsync(resultSetId, jar), "result_set.needs_recompute");
    }

    [Fact]
    public async Task Publish_WithoutALogo_Returns409LogoMissing()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedAsync(withImages: false);

        await ShouldFailAsync(await PublishAsync(resultSetId, jar), "result_set.logo_missing");
    }

    [Fact]
    public async Task Publish_WithoutANextResumptionDate_Returns409()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedAsync(withResumptionDate: false);

        await ShouldFailAsync(await PublishAsync(resultSetId, jar), "result_set.next_resumption_date_missing");
    }

    [Fact]
    public async Task Publish_ThirdTerm_WithTheSeededRulesNamingNoCoreSubjects_Returns409()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedAsync(termOrdinal: 3);

        await ShouldFailAsync(await PublishAsync(resultSetId, jar), "result_set.core_subjects_missing");
    }

    [Fact]
    public async Task Publish_WithoutAnIdempotencyKey_Returns400()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedAsync();

        var response = await PublishAsync(resultSetId, jar, idempotencyKey: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Withdraw_Reopen_Republish_KeepsBothSnapshotsAndReachesRevisionTwo()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedAsync();
        (await PublishAsync(resultSetId, jar)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var withdraw = await PostAsync($"/api/v1/result-sets/{resultSetId}/withdraw", jar, new { reason = "Mathematics marks were entered for the wrong class." });
        withdraw.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<ResultSetTransitionResponse>(withdraw)).ResultSet.State.ShouldBe(ResultSetState.Withdrawn);

        var reopen = await PostAsync($"/api/v1/result-sets/{resultSetId}/reopen", jar, payload: null);
        reopen.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reopened = (await ReadAsync<ResultSetTransitionResponse>(reopen)).ResultSet;
        reopened.State.ShouldBe(ResultSetState.Draft);
        reopened.NeedsRecompute.ShouldBeTrue();

        // Stand in for recompute, resubmit and re-approve, which have their own tests.
        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var resultSet = await context.ResultSets.SingleAsync(r => r.Id == resultSetId, TestContext.Current.CancellationToken);
            resultSet.MarkComputed(null, DateTimeOffset.UtcNow, pupilCount: 0);
            typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, ResultSetState.Approved);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var republish = await PublishAsync(resultSetId, jar);
        republish.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<PublishResultSetResponse>(republish)).RevisionNumber.ShouldBe(2);

        await using var verify = Fixture.CreateScope();
        var snapshots = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<ResultSetSnapshot>()
            .AsNoTracking().Where(snapshot => snapshot.ResultSetId == resultSetId)
            .OrderBy(snapshot => snapshot.RevisionNumber).Select(snapshot => snapshot.RevisionNumber)
            .ToListAsync(TestContext.Current.CancellationToken);
        snapshots.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task Withdraw_ASetThatIsNotPublished_Returns409()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedAsync();

        var response = await PostAsync($"/api/v1/result-sets/{resultSetId}/withdraw", jar, new { reason = "Pulling this before it was ever published." });

        await ShouldFailAsync(response, "result_set.not_published");
    }

    [Fact]
    public async Task Withdraw_WithAShortReason_Returns422()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedAsync(state: ResultSetState.Published);

        var response = await PostAsync($"/api/v1/result-sets/{resultSetId}/withdraw", jar, new { reason = "Too short" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Reopen_ASetThatIsNotWithdrawn_Returns409()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedAsync(state: ResultSetState.Published, termActive: true);

        await ShouldFailAsync(await PostAsync($"/api/v1/result-sets/{resultSetId}/reopen", jar, payload: null), "result_set.not_withdrawn");
    }

    [Fact]
    public async Task Reopen_WhenTheTermIsNotActive_Returns409()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedAsync(state: ResultSetState.Withdrawn, termActive: false);

        await ShouldFailAsync(await PostAsync($"/api/v1/result-sets/{resultSetId}/reopen", jar, payload: null), "result_set.term_not_active");
    }

    private Task<HttpResponseMessage> PostAsync(string url, CookieJar jar, object? payload) =>
        SendAsync(HttpMethod.Post, url, jar, payload is null ? null : JsonContent.Create(payload), csrf: true);

    private static async Task ShouldFailAsync(HttpResponseMessage response, string errorCode)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("errorCode").GetString().ShouldBe(errorCode);
    }

    private async Task<(Guid ResultSetId, CookieJar Jar)> SeedAsync(
        ResultSetState state = ResultSetState.Approved,
        bool needsRecompute = false,
        bool withImages = true,
        bool withResumptionDate = true,
        int termOrdinal = 1,
        bool termActive = true)
    {
        var (accountId, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false, email: $"admin-{Guid.NewGuid():N}@example.com");

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var levelId = (await context.ClassLevels.AsNoTracking()
                .FirstAsync(level => level.SectionId == SeededClassLevels.PrimarySectionId, TestContext.Current.CancellationToken)).Id;

            var year = Interlocked.Increment(ref _nextSessionStartYear);
            var session = AcademicSession.Create(
                Guid.CreateVersion7(), $"{year}/{year + 1}", new DateOnly(year, 9, 1), new DateOnly(year + 1, 7, 31)).Value;
            session.Activate();
            context.Add(session);

            var start = new DateOnly(year, 9, 1).AddMonths(4 * (termOrdinal - 1));
            var term = Term.Create(Guid.CreateVersion7(), session.Id, termOrdinal, $"Term {termOrdinal}", start, start.AddMonths(3)).Value;
            term.SetTimesSchoolOpened(58).IsSuccess.ShouldBeTrue();
            if (termActive)
            {
                term.Open().IsSuccess.ShouldBeTrue();
            }

            if (withResumptionDate)
            {
                term.UpdateSchedule(term.Name, term.StartDate, term.EndDate, term.EndDate.AddDays(20)).IsSuccess.ShouldBeTrue();
            }

            context.Add(term);

            var arm = Arm.Create(Guid.CreateVersion7(), levelId, session.Id, "A", null, accountId).Value;
            context.Add(arm);

            var resultSet = ResultSet.Create(Guid.CreateVersion7(), arm.Id, term.Id).Value;
            if (!needsRecompute)
            {
                resultSet.MarkComputed(accountId, DateTimeOffset.UtcNow, pupilCount: 0);
            }

            typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, state);
            context.Add(resultSet);

            if (withImages)
            {
                var profile = await context.Set<SchoolProfile>().SingleAsync(TestContext.Current.CancellationToken);
                profile.SetCurrentLogo(Guid.CreateVersion7());
                profile.SetCurrentSignature(Guid.CreateVersion7());
            }

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var jar = new CookieJar();
            await SendAsync(HttpMethod.Get, CsrfUrl, jar, content: null, csrf: false);
            var signIn = await SendAsync(HttpMethod.Post, SignInUrl, jar, JsonContent.Create(new SignInCommand(email, AdminAccountSeeder.Password)), csrf: true);
            signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
            return (resultSet.Id, jar);
        }
    }

    private Task<HttpResponseMessage> PublishAsync(Guid resultSetId, CookieJar jar, string? idempotencyKey = "generate") =>
        SendAsync(HttpMethod.Post, $"/api/v1/result-sets/{resultSetId}/publish", jar, content: null, csrf: true,
            idempotencyKey == "generate" ? Guid.NewGuid().ToString("D") : idempotencyKey);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, CookieJar jar, HttpContent? content, bool csrf, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        if (csrf)
        {
            jar.ApplyWithCsrf(request);
        }
        else
        {
            jar.Apply(request);
        }

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
