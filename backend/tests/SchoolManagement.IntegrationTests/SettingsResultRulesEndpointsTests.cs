using System.Net;
using System.Net.Http.Json;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0077's endpoints against the approved contract delta (amended 2026-09-18
/// to add <c>expectedVersion</c>/<c>versionNumber</c> optimistic concurrency, matching
/// <c>UpdateAssessmentCommandHandler</c>'s own precedent): the seeded result rules on a fresh database
/// (spec 6.2.8, `coreSubjectIds` empty, `versionNumber` 0), the whole-row save with its field-shape
/// validation, the stale-version 409 (same pattern as
/// <c>SettingsGradingAssessmentEndpointsTests.UpdateGrading_WithAStaleExpectedVersion_Returns409</c>),
/// and the authorisation/CSRF boundary. The two 6.2.10 hard locks are proven at the unit level
/// (<c>UpdateResultRulesCommandHandlerTests</c>) — same allocation as
/// <c>SettingsGradingAssessmentEndpointsTests</c> makes for the 6.2.9 reason gate, since publishing a
/// result set has no endpoint yet to drive one through here.
/// </summary>
public sealed class SettingsResultRulesEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string ResultRulesUrl = "/api/v1/settings/result-rules";

    [Fact]
    public async Task GetResultRules_OnAFreshDatabase_ReturnsSpec628SSeededDefaults()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var resultRules = await ReadAsync<ResultRulesDto>(await GetAsync(ResultRulesUrl, jar));

        resultRules.AnnualMethod.ShouldBe(AnnualMethod.SimpleAverage);
        resultRules.WeightFirst.ShouldBeNull();
        resultRules.PrimaryPositionScope.ShouldBe(PrimaryPositionScope.Arm);
        resultRules.ShowLevelPosition.ShouldBeTrue();
        resultRules.TieBreakRule.ShouldBe(TieBreakRule.SharedPosition);
        resultRules.PassMark.ShouldBe(40);
        resultRules.PromotionThreshold.ShouldBe(40);
        resultRules.RequireCorePass.ShouldBeTrue();
        resultRules.CoreSubjectIds.ShouldBeEmpty();
        resultRules.MinSubjectsForPosition.ShouldBe(1);
        resultRules.VersionNumber.ShouldBe(0);
    }

    [Fact]
    public async Task GetResultRules_WhileAnonymous_Returns401()
    {
        RequireDatabase();

        var response = await Client.GetAsync(ResultRulesUrl, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateResultRules_HappyPath_ReplacesTheRowAndIsReflectedOnTheNextGet()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var command = new UpdateResultRulesCommand(
            AnnualMethod.Weighted,
            WeightFirst: 30,
            WeightSecond: 30,
            WeightThird: 40,
            PrimaryPositionScope.Level,
            ShowLevelPosition: false,
            TieBreakRule.ExamThenCa,
            PassMark: 45,
            PromotionThreshold: 50,
            RequireCorePass: false,
            CoreSubjectIds: [],
            MinSubjectsForPosition: 2,
            ExpectedVersion: 0,
            Reason: null);

        var response = await PutAsync(ResultRulesUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadAsync<ResultRulesDto>(response);
        updated.AnnualMethod.ShouldBe(AnnualMethod.Weighted);
        updated.WeightFirst.ShouldBe(30);
        updated.PrimaryPositionScope.ShouldBe(PrimaryPositionScope.Level);
        updated.PassMark.ShouldBe(45);
        updated.VersionNumber.ShouldBe(1);

        var reread = await ReadAsync<ResultRulesDto>(await GetAsync(ResultRulesUrl, jar));
        reread.AnnualMethod.ShouldBe(AnnualMethod.Weighted);
        reread.MinSubjectsForPosition.ShouldBe(2);
        reread.VersionNumber.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateResultRules_WithAStaleExpectedVersion_Returns409()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var firstResponse = await PutAsync(ResultRulesUrl, jar, DefaultCommand(expectedVersion: 0));
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Resubmits the now-stale version 0 — the first save already advanced it to 1.
        var secondResponse = await PutAsync(ResultRulesUrl, jar, DefaultCommand(expectedVersion: 0));

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(secondResponse);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.resultrules.stale_version");

        // The rejected save wrote nothing — a re-read still shows the FIRST save's values, not a
        // second, silently-applied one.
        var reread = await ReadAsync<ResultRulesDto>(await GetAsync(ResultRulesUrl, jar));
        reread.VersionNumber.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateResultRules_WeightedWithWeightsNotTotallingAHundred_Returns422()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var command = new UpdateResultRulesCommand(
            AnnualMethod.Weighted,
            WeightFirst: 30,
            WeightSecond: 30,
            WeightThird: 30,
            PrimaryPositionScope.Arm,
            ShowLevelPosition: true,
            TieBreakRule.SharedPosition,
            PassMark: 40,
            PromotionThreshold: 40,
            RequireCorePass: false,
            CoreSubjectIds: [],
            MinSubjectsForPosition: 1,
            ExpectedVersion: 0,
            Reason: null);

        var response = await PutAsync(ResultRulesUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateResultRules_WithoutACsrfToken_Returns403CsrfMissing()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var command = DefaultCommand(expectedVersion: 0);

        using var request = new HttpRequestMessage(HttpMethod.Put, ResultRulesUrl)
        {
            Content = JsonContent.Create(command),
        };
        jar.Apply(request); // Cookies, but deliberately no X-CSRF-Token.

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("csrf.missing");
    }

    private static UpdateResultRulesCommand DefaultCommand(int expectedVersion) => new(
        AnnualMethod.SimpleAverage,
        WeightFirst: null,
        WeightSecond: null,
        WeightThird: null,
        PrimaryPositionScope.Arm,
        ShowLevelPosition: true,
        TieBreakRule.SharedPosition,
        PassMark: 40,
        PromotionThreshold: 40,
        RequireCorePass: false,
        CoreSubjectIds: [],
        MinSubjectsForPosition: 1,
        expectedVersion,
        Reason: null);

    private async Task<CookieJar> SignInAsSuperAdminAsync()
    {
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private Task<HttpResponseMessage> GetAsync(string url, CookieJar jar) => GetAsync(Client, url, jar);

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload) =>
        PostAsync(Client, url, jar, payload);

    private static async Task<HttpResponseMessage> PostAsync<T>(HttpClient client, string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };

        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PutAsync<T>(string url, CookieJar jar, T payload) =>
        PutAsync(Client, url, jar, payload);

    private static async Task<HttpResponseMessage> PutAsync<T>(HttpClient client, string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(payload),
        };

        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
