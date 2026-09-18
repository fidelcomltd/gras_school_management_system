using System.Net;
using System.Net.Http.Json;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0069's endpoints against the approved contract delta: the seeded nine
/// grading bands and 20/20/60 assessment structure on a fresh database (spec 6.2.13), the whole-scale
/// atomic replace with its ten/six numbered validation rules, optimistic concurrency, the reset path,
/// and that the second named profile (<c>with_assignment</c>) saves through the SAME endpoint with no
/// special-casing (6.2.13: "nothing later branches on which was chosen").
/// </summary>
public sealed class SettingsGradingAssessmentEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string SettingsUrl = "/api/v1/settings";
    private const string GradingUrl = "/api/v1/settings/grading";
    private const string GradingResetUrl = "/api/v1/settings/grading/reset";
    private const string AssessmentUrl = "/api/v1/settings/assessment";

    [Fact]
    public async Task GetSettings_OnAFreshDatabase_ReturnsTheNineSeededGradingBandsInPrintOrder()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));

        settings.Grading.VersionNumber.ShouldBe(0);
        settings.Grading.Bands.Select(band => band.GradeLetter).ShouldBe(
            ["A+", "A", "B", "B-", "C+", "C", "D", "E", "F"]);
        settings.Grading.Bands.First(band => band.GradeLetter == "A+").LowerBound.ShouldBe(90);
        settings.Grading.Bands.First(band => band.GradeLetter == "F").UpperBound.ShouldBe(19);
    }

    [Fact]
    public async Task GetSettings_OnAFreshDatabase_ReturnsTheSeededGrasDefaultAssessmentStructure()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));

        settings.Assessment.VersionNumber.ShouldBe(0);
        settings.Assessment.Components.Count.ShouldBe(3);
        settings.Assessment.Components.Select(component => component.Name).ShouldBe(["1st CA", "2nd CA", "Exam"]);
        settings.Assessment.Components.Sum(component => component.MaxMark).ShouldBe(100);
        settings.Assessment.Components[^1].IsExamination.ShouldBeTrue(); // Exam is always last.
    }

    [Fact]
    public async Task UpdateGrading_WhileAnonymous_Returns401()
    {
        RequireDatabase();

        using var request = new HttpRequestMessage(HttpMethod.Put, GradingUrl)
        {
            Content = JsonContent.Create(TwoBandScale(expectedVersion: 0)),
        };

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateGrading_HappyPath_ReplacesTheScaleAndReturnsTheIncrementedVersion()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await PutAsync(GradingUrl, jar, TwoBandScale(expectedVersion: 0));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var grading = await ReadAsync<SettingsGradingGroupDto>(response);
        grading.VersionNumber.ShouldBe(1);
        grading.Bands.Count.ShouldBe(2);

        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        settings.Grading.Bands.Count.ShouldBe(2);
        settings.Grading.VersionNumber.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateGrading_WithAGapInCoverage_Returns422NamingTheOffendingBandIndex()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var command = new UpdateGradingCommand(
            [
                new GradingBandInput(0, 49, "F", "Fail"),
                new GradingBandInput(61, 100, "P", "Pass"), // gap: mark 50-60 belongs to no band.
            ],
            ExpectedVersion: 0,
            Reason: null);

        var response = await PutAsync(GradingUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe(GradingScaleRules.CoverageGapCode);
        document.RootElement.TryGetProperty("bandIndex", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateGrading_WithAStaleExpectedVersion_Returns409()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var firstResponse = await PutAsync(GradingUrl, jar, TwoBandScale(expectedVersion: 0));
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondResponse = await PutAsync(GradingUrl, jar, TwoBandScale(expectedVersion: 0));

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(secondResponse);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.grading.stale_version");
    }

    [Fact]
    public async Task ResetGrading_AfterAnEdit_RestoresExactlyTheNineSeededBands()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var edited = await PutAsync(GradingUrl, jar, TwoBandScale(expectedVersion: 0));
        edited.StatusCode.ShouldBe(HttpStatusCode.OK);

        var resetResponse = await PostAsync(GradingResetUrl, jar, new ResetGradingCommand(ExpectedVersion: 1, Reason: null));

        resetResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var grading = await ReadAsync<SettingsGradingGroupDto>(resetResponse);
        grading.Bands.Count.ShouldBe(9);
        grading.Bands.Select(band => band.GradeLetter).ShouldBe(
            ["A+", "A", "B", "B-", "C+", "C", "D", "E", "F"]);
    }

    [Fact]
    public async Task UpdateAssessment_WithTheWithAssignmentProfile_SavesThroughTheOrdinaryEndpoint()
    {
        RequireDatabase();

        // 6.2.13: the second named profile is reachable through the SAME PUT, unmodified, proving
        // "nothing later branches on which was chosen" — no special profile parameter exists to branch on.
        var jar = await SignInAsSuperAdminAsync();

        var command = new UpdateAssessmentCommand(
            [
                new AssessmentComponentSaveRequest(null, "1st CA", "CA1", 15, false),
                new AssessmentComponentSaveRequest(null, "2nd CA", "CA2", 15, false),
                new AssessmentComponentSaveRequest(null, "Assignment", "ASSGN", 10, false),
                new AssessmentComponentSaveRequest(null, "Exam", "EXAM", 60, true),
            ],
            ExpectedVersion: 0,
            Reason: null);

        var response = await PutAsync(AssessmentUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var assessment = await ReadAsync<SettingsAssessmentGroupDto>(response);
        assessment.Components.Count.ShouldBe(4);
        assessment.Components.Sum(component => component.MaxMark).ShouldBe(100);
        assessment.Components[^1].IsExamination.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateAssessment_WithComponentsNotTotallingAHundred_Returns422()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var command = new UpdateAssessmentCommand(
            [
                new AssessmentComponentSaveRequest(null, "CA", "CA", 30, false),
                new AssessmentComponentSaveRequest(null, "Exam", "EXAM", 60, true),
            ],
            ExpectedVersion: 0,
            Reason: null);

        var response = await PutAsync(AssessmentUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe(AssessmentStructureRules.DoesNotTotalHundredCode);
    }

    [Fact]
    public async Task UpdateAssessment_WithoutACsrfToken_Returns403CsrfMissing()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        using var request = new HttpRequestMessage(HttpMethod.Put, AssessmentUrl)
        {
            Content = JsonContent.Create(new UpdateAssessmentCommand([], ExpectedVersion: 0, Reason: null)),
        };
        jar.Apply(request); // Cookies, but deliberately no X-CSRF-Token.

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("csrf.missing");
    }

    private static UpdateGradingCommand TwoBandScale(int expectedVersion) => new(
        [
            new GradingBandInput(50, 100, "P", "Pass"),
            new GradingBandInput(0, 49, "F", "Fail"),
        ],
        expectedVersion,
        null);

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
