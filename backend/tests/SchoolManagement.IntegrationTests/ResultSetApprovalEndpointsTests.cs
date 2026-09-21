using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0090's approve and return endpoints (spec §6.7.8, §6.7.11): the happy
/// paths, every 409 the contract delta names, the 422 reason bounds, and that an unknown id is 403.
/// Neither route needs a "ready" set the way submit does (spec 6.7.11: approval's own precondition is
/// "none beyond the state"; return's is only the reason) so seeding here is a bare result set in the
/// state under test, not <c>ResultSetSubmitEndpointsTests</c>'s full sheet round trip.
/// </summary>
public sealed class ResultSetApprovalEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string ValidReason = "Mathematics examination marks for the whole class look 10 marks too low.";

    private static int _nextSessionStartYear = 9900;

    // ---- Approve happy path and refusals -----------------------------------------------------------

    [Fact]
    public async Task Approve_AnAwaitingApprovalSet_MovesToApprovedAndReturns200()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedResultSetAsync(ResultSetState.AwaitingApproval);

        var response = await ApproveAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ApproveResultSetResponse>(response);
        body.ResultSet.State.ShouldBe(ResultSetState.Approved);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.AsNoTracking()
            .SingleAsync(r => r.Id == resultSetId, TestContext.Current.CancellationToken);
        resultSet.State.ShouldBe(ResultSetState.Approved);
        resultSet.ApprovedAtUtc.ShouldNotBeNull();
        resultSet.ApprovedBy.ShouldNotBeNull();
    }

    [Fact]
    public async Task Approve_WhenNeedsRecomputeIsSet_Returns409()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedResultSetAsync(ResultSetState.AwaitingApproval, needsRecompute: true);

        var response = await ApproveAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("result_set.needs_recompute");
    }

    [Fact]
    public async Task Approve_FromDraft_Returns409()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedResultSetAsync(ResultSetState.Draft);

        var response = await ApproveAsync(resultSetId, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("result_set.not_awaiting_approval");
    }

    [Fact]
    public async Task Approve_AnUnknownResultSetId_Returns403()
    {
        RequireDatabase();
        var jar = await SignInAsSuperAdminAsync();

        var response = await ApproveAsync(Guid.CreateVersion7(), jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- Return happy paths and refusals -----------------------------------------------------------

    [Theory]
    [InlineData(ResultSetState.AwaitingApproval)]
    [InlineData(ResultSetState.Approved)]
    public async Task Return_AnAwaitingApprovalOrApprovedSet_MovesToReturnedForCorrectionAndReturns200(ResultSetState fromState)
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedResultSetAsync(fromState);

        var response = await ReturnAsync(resultSetId, jar, ValidReason);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ReturnResultSetResponse>(response);
        body.ResultSet.State.ShouldBe(ResultSetState.ReturnedForCorrection);
        body.ResultSet.ReturnReason.ShouldBe(ValidReason);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.AsNoTracking()
            .SingleAsync(r => r.Id == resultSetId, TestContext.Current.CancellationToken);
        resultSet.State.ShouldBe(ResultSetState.ReturnedForCorrection);
        resultSet.ReturnReason.ShouldBe(ValidReason);
    }

    [Fact]
    public async Task Return_ASecondTime_OverwritesThePriorReason()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedResultSetAsync(ResultSetState.AwaitingApproval);
        var first = await ReturnAsync(resultSetId, jar, ValidReason);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        // A returned set is not resubmitted here — the state stays Returned for Correction, which
        // this route also accepts (spec 6.7.8: "An Approved set can also be returned"; the SAME state
        // is not one of the two allowed FROM states, so re-seed it back to Approved for the second call).
        await SetResultSetStateAsync(resultSetId, ResultSetState.Approved);
        var secondReason = "A different, second reason for returning this same set.";

        var second = await ReturnAsync(resultSetId, jar, secondReason);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<ReturnResultSetResponse>(second);
        body.ResultSet.ReturnReason.ShouldBe(secondReason);
    }

    [Theory]
    [InlineData("Too short.")]
    [InlineData("")]
    public async Task Return_WithATooShortReason_Returns422(string reason)
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedResultSetAsync(ResultSetState.AwaitingApproval);

        var response = await ReturnAsync(resultSetId, jar, reason);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Return_WithAReasonOverFiveHundredCharacters_Returns422()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedResultSetAsync(ResultSetState.AwaitingApproval);

        var response = await ReturnAsync(resultSetId, jar, new string('x', 501));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Return_FromDraft_Returns409()
    {
        RequireDatabase();
        var (resultSetId, jar) = await SeedResultSetAsync(ResultSetState.Draft);

        var response = await ReturnAsync(resultSetId, jar, ValidReason);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("result_set.not_returnable");
    }

    [Fact]
    public async Task Return_AnUnknownResultSetId_Returns403()
    {
        RequireDatabase();
        var jar = await SignInAsSuperAdminAsync();

        var response = await ReturnAsync(Guid.CreateVersion7(), jar, ValidReason);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- Seeding and HTTP helpers -------------------------------------------------------------------

    /// <summary>
    /// A bare result set in <paramref name="state"/> — one session, term and arm, no marks. Neither
    /// approve nor return re-checks the completeness gate (spec 6.7.11), so nothing more is needed.
    /// </summary>
    private async Task<(Guid ResultSetId, CookieJar Jar)> SeedResultSetAsync(ResultSetState state, bool needsRecompute = false)
    {
        var (accountId, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false, email: $"admin-{Guid.NewGuid():N}@example.com");

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.SectionId == SeededClassLevels.PrimarySectionId, TestContext.Current.CancellationToken)).Id;

        var startYear = Interlocked.Increment(ref _nextSessionStartYear);
        var session = AcademicSession.Create(
            Guid.CreateVersion7(), $"{startYear}/{startYear + 1}",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25)).Value;
        session.Activate();
        context.Add(session);

        var term = Term.Create(
            Guid.CreateVersion7(), session.Id, 1, "First Term",
            new DateOnly(startYear, 9, 14), new DateOnly(startYear, 12, 12)).Value;
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

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var jar = new CookieJar();
        await GetAsyncCore(CsrfUrl, jar, Client);
        var signIn = await PostAsyncCore(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password), Client);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (resultSet.Id, jar);
    }

    private async Task SetResultSetStateAsync(Guid resultSetId, ResultSetState state)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resultSet = await context.ResultSets.SingleAsync(r => r.Id == resultSetId, TestContext.Current.CancellationToken);
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, state);
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

    private Task<HttpResponseMessage> ApproveAsync(Guid resultSetId, CookieJar jar) =>
        Client.SendAsync(BuildPostRequestNoBody($"/api/v1/result-sets/{resultSetId}/approve", jar), TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> ReturnAsync(Guid resultSetId, CookieJar jar, string reason) =>
        Client.SendAsync(BuildPostRequest($"/api/v1/result-sets/{resultSetId}/return", jar, new { reason }), TestContext.Current.CancellationToken);

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
