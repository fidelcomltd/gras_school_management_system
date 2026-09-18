using System.Net;
using System.Net.Http.Json;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0072 stage 1's endpoint against the approved contract delta: the three
/// seeded rating scales on a fresh database (spec 6.2.13), the whole-set atomic replace with its
/// save-time rules, optimistic concurrency, and that removing a scale nothing yet references succeeds
/// (the <c>settings.ratingscales.in_use</c> refusal has no reference table to exercise it against
/// until TASK-0072 stages 2/3 land — see <c>SchoolManagement.Infrastructure.Settings.RatingScaleUsageGate</c>'s remarks).
/// </summary>
public sealed class SettingsRatingScalesEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string SettingsUrl = "/api/v1/settings";
    private const string RatingScalesUrl = "/api/v1/settings/rating-scales";

    [Fact]
    public async Task GetSettings_OnAFreshDatabase_ReturnsTheThreeSeededRatingScales()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));

        settings.RatingScales.VersionNumber.ShouldBe(0);
        settings.RatingScales.Scales.Select(scale => scale.Name).ShouldBe(
            [RatingScaleSeed.FivePointNumericName, RatingScaleSeed.NurseryDevelopmentName, RatingScaleSeed.PrimaryTraitName]);

        var nursery = settings.RatingScales.Scales.Single(scale => scale.Name == RatingScaleSeed.NurseryDevelopmentName);
        nursery.Points.Select(point => point.PointCode).ShouldBe(["N", "I", "S", "E"]);

        var primary = settings.RatingScales.Scales.Single(scale => scale.Name == RatingScaleSeed.PrimaryTraitName);
        primary.Points.Select(point => point.PointCode).ShouldBe(["N", "I", "E"]);

        var fivePoint = settings.RatingScales.Scales.Single(scale => scale.Name == RatingScaleSeed.FivePointNumericName);
        fivePoint.Points.Count.ShouldBe(5);
    }

    [Fact]
    public async Task UpdateRatingScales_WhileAnonymous_Returns401()
    {
        RequireDatabase();

        using var request = new HttpRequestMessage(HttpMethod.Put, RatingScalesUrl)
        {
            Content = JsonContent.Create(CustomScale(expectedVersion: 0)),
        };

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateRatingScales_HappyPath_ReplacesTheSetAndReturnsTheIncrementedVersion()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var response = await PutAsync(RatingScalesUrl, jar, CustomScale(expectedVersion: 0));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ratingScales = await ReadAsync<SettingsRatingScaleGroupDto>(response);
        ratingScales.VersionNumber.ShouldBe(1);
        ratingScales.Scales.Count.ShouldBe(1);
        ratingScales.Scales[0].Name.ShouldBe("Custom");

        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        settings.RatingScales.Scales.Count.ShouldBe(1);
        settings.RatingScales.VersionNumber.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateRatingScales_WithOnlyOnePoint_Returns422NamingTheOffendingScaleIndex()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var command = new UpdateRatingScalesCommand(
            [new RatingScaleInput("Too small", [new RatingScalePointInput("E", "Excellent", 1)])],
            ExpectedVersion: 0,
            Reason: null);

        var response = await PutAsync(RatingScalesUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe(RatingScaleRules.PointCountInvalidCode);
        document.RootElement.GetProperty("scaleIndex").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task UpdateRatingScales_WithAStaleExpectedVersion_Returns409()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var firstResponse = await PutAsync(RatingScalesUrl, jar, CustomScale(expectedVersion: 0));
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondResponse = await PutAsync(RatingScalesUrl, jar, CustomScale(expectedVersion: 0));

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(secondResponse);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.ratingscales.stale_version");
    }

    [Fact]
    public async Task UpdateRatingScales_RemovingASeededScale_SucceedsBecauseNothingYetReferencesIt()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        // The submitted set omits all three seeded scales — allowed today because TASK-0072 stage 1
        // has no development-domain or trait-block table to reference one yet.
        var response = await PutAsync(RatingScalesUrl, jar, CustomScale(expectedVersion: 0));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ratingScales = await ReadAsync<SettingsRatingScaleGroupDto>(response);
        ratingScales.Scales.ShouldNotContain(scale => scale.Name == RatingScaleSeed.NurseryDevelopmentName);
    }

    private static UpdateRatingScalesCommand CustomScale(int expectedVersion) => new(
        [new RatingScaleInput("Custom", [new RatingScalePointInput("N", "Needs Improvement", 1), new RatingScalePointInput("E", "Excellent", 2)])],
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
