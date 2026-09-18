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
/// save-time rules, and optimistic concurrency. TASK-0072 stage 2a makes
/// <c>SchoolManagement.Infrastructure.Settings.RatingScaleUsageGate</c> a real query against
/// <c>development_domain</c>, so the seeded Nursery development scale is now referenced by the seeded
/// nursery development domains — every test below that needs a clean scale to mutate echoes it back
/// unchanged, and two tests prove the in-use gate itself in both directions (removing an unreferenced
/// scale succeeds; removing the referenced one is refused 409).
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
            Content = JsonContent.Create(new UpdateRatingScalesCommand([CustomScale()], ExpectedVersion: 0, Reason: null)),
        };

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateRatingScales_HappyPath_ReplacesTheSetAndReturnsTheIncrementedVersion()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var command = await PreservingNurseryDevelopmentAsync(jar, CustomScale(), expectedVersion: 0);
        var response = await PutAsync(RatingScalesUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ratingScales = await ReadAsync<SettingsRatingScaleGroupDto>(response);
        ratingScales.VersionNumber.ShouldBe(1);
        ratingScales.Scales.Count.ShouldBe(2);
        ratingScales.Scales.ShouldContain(scale => scale.Name == "Custom");
        ratingScales.Scales.ShouldContain(scale => scale.Name == RatingScaleSeed.NurseryDevelopmentName);

        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        settings.RatingScales.Scales.Count.ShouldBe(2);
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

        var firstCommand = await PreservingNurseryDevelopmentAsync(jar, CustomScale(), expectedVersion: 0);
        var firstResponse = await PutAsync(RatingScalesUrl, jar, firstCommand);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondResponse = await PutAsync(RatingScalesUrl, jar, firstCommand);

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(secondResponse);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.ratingscales.stale_version");
    }

    [Fact]
    public async Task UpdateRatingScales_RemovingAnUnreferencedSeededScale_Succeeds()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        // The submitted set echoes Nursery development back unchanged (referenced by the seeded
        // nursery development domains, TASK-0072 stage 2a) but omits Primary trait and Five-point
        // numeric — both genuinely unreferenced, so removing them succeeds.
        var command = await PreservingNurseryDevelopmentAsync(jar, CustomScale(), expectedVersion: 0);
        var response = await PutAsync(RatingScalesUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ratingScales = await ReadAsync<SettingsRatingScaleGroupDto>(response);
        ratingScales.Scales.ShouldContain(scale => scale.Name == RatingScaleSeed.NurseryDevelopmentName);
        ratingScales.Scales.ShouldNotContain(scale => scale.Name == RatingScaleSeed.PrimaryTraitName);
        ratingScales.Scales.ShouldNotContain(scale => scale.Name == RatingScaleSeed.FivePointNumericName);
    }

    [Fact]
    public async Task UpdateRatingScales_RemovingTheReferencedSeededScale_Returns409InUse()
    {
        // The positive control for the gate becoming real in TASK-0072 stage 2a: without this, a
        // bug that refused every removal for ANY reason would still make the test above pass.
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var command = new UpdateRatingScalesCommand([CustomScale()], ExpectedVersion: 0, Reason: null);
        var response = await PutAsync(RatingScalesUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.ratingscales.in_use");
    }

    [Fact]
    public async Task UpdateRatingScales_ResavedWithTheSameEchoedIds_KeepsEveryScaleAndPointIdStable()
    {
        // The stage-1 review fix this proves: stage 2/3 rating blocks will store a rating_scale_id
        // FK, so a resave that changes nothing must not silently mint new ids underneath it.
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var createCommand = await PreservingNurseryDevelopmentAsync(jar, CustomScale(), expectedVersion: 0);
        var created = await ReadAsync<SettingsRatingScaleGroupDto>(await PutAsync(RatingScalesUrl, jar, createCommand));
        var custom = created.Scales.Single(scale => scale.Name == "Custom");
        var scaleId = custom.Id;
        var pointIds = custom.Points.Select(point => point.Id).ToList();
        var nursery = createCommand.Scales.Single(scale => scale.Name == RatingScaleSeed.NurseryDevelopmentName);

        var resubmit = new UpdateRatingScalesCommand(
            [
                nursery,
                new RatingScaleInput(
                    "Custom",
                    [
                        new RatingScalePointInput("N", "Needs Improvement", 1, Id: Guid.Parse(pointIds[0])),
                        new RatingScalePointInput("E", "Excellent", 2, Id: Guid.Parse(pointIds[1])),
                    ],
                    Id: Guid.Parse(scaleId)),
            ],
            ExpectedVersion: 1,
            Reason: null);

        var response = await PutAsync(RatingScalesUrl, jar, resubmit);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resaved = await ReadAsync<SettingsRatingScaleGroupDto>(response);
        resaved.VersionNumber.ShouldBe(2);
        var resavedCustom = resaved.Scales.Single(scale => scale.Name == "Custom");
        resavedCustom.Id.ShouldBe(scaleId);
        resavedCustom.Points.Select(point => point.Id).ShouldBe(pointIds, ignoreOrder: true);
    }

    [Fact]
    public async Task UpdateRatingScales_RenamingByEchoedId_KeepsTheId()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var createCommand = await PreservingNurseryDevelopmentAsync(jar, CustomScale(), expectedVersion: 0);
        var created = await ReadAsync<SettingsRatingScaleGroupDto>(await PutAsync(RatingScalesUrl, jar, createCommand));
        var custom = created.Scales.Single(scale => scale.Name == "Custom");
        var scaleId = custom.Id;
        var pointIds = custom.Points.Select(point => point.Id).ToList();
        var nursery = createCommand.Scales.Single(scale => scale.Name == RatingScaleSeed.NurseryDevelopmentName);

        var rename = new UpdateRatingScalesCommand(
            [
                nursery,
                new RatingScaleInput(
                    "Renamed",
                    [
                        new RatingScalePointInput("N", "Needs Improvement", 1, Id: Guid.Parse(pointIds[0])),
                        new RatingScalePointInput("E", "Excellent", 2, Id: Guid.Parse(pointIds[1])),
                    ],
                    Id: Guid.Parse(scaleId)),
            ],
            ExpectedVersion: 1,
            Reason: null);

        var response = await PutAsync(RatingScalesUrl, jar, rename);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var renamed = await ReadAsync<SettingsRatingScaleGroupDto>(response);
        var renamedScale = renamed.Scales.Single(scale => scale.Id == scaleId);
        renamedScale.Name.ShouldBe("Renamed");
    }

    [Fact]
    public async Task UpdateRatingScales_WithAnUnknownScaleId_Returns422NamingTheScaleIndex()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var command = new UpdateRatingScalesCommand(
            [
                new RatingScaleInput(
                    "Custom",
                    [new RatingScalePointInput("N", "Needs Improvement", 1), new RatingScalePointInput("E", "Excellent", 2)],
                    Id: Guid.CreateVersion7()),
            ],
            ExpectedVersion: 0,
            Reason: null);

        var response = await PutAsync(RatingScalesUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.ratingscales.unknown_scale_id");
        document.RootElement.GetProperty("scaleIndex").GetInt32().ShouldBe(0);
    }

    // TASK-0072 stage 2a: the seeded "Nursery development" scale is now referenced by the seeded
    // nursery development domains (RatingScaleUsageGate is a real query from this stage on), so ANY
    // submission that omits it is refused 409 settings.ratingscales.in_use. These tests are about the
    // rating-scales group's own behaviour (version, id stability, removing an UNREFERENCED scale), not
    // about the in-use gate itself, so every submission below echoes the Nursery development scale back
    // unchanged (by id) alongside whatever it is actually testing.
    private static RatingScaleInput CustomScale() =>
        new("Custom", [new RatingScalePointInput("N", "Needs Improvement", 1), new RatingScalePointInput("E", "Excellent", 2)]);

    private static RatingScaleInput ToInput(RatingScaleDto scale) => new(
        scale.Name,
        scale.Points
            .Select(point => new RatingScalePointInput(point.PointCode, point.PointLabel, point.PointOrder, Guid.Parse(point.Id)))
            .ToList(),
        Guid.Parse(scale.Id));

    private async Task<RatingScaleInput> GetUnchangedNurseryDevelopmentScaleAsync(CookieJar jar)
    {
        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        var nursery = settings.RatingScales.Scales.Single(scale => scale.Name == RatingScaleSeed.NurseryDevelopmentName);
        return ToInput(nursery);
    }

    private async Task<UpdateRatingScalesCommand> PreservingNurseryDevelopmentAsync(
        CookieJar jar, RatingScaleInput scale, int expectedVersion)
    {
        var nursery = await GetUnchangedNurseryDevelopmentScaleAsync(jar);
        return new UpdateRatingScalesCommand([nursery, scale], expectedVersion, null);
    }

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
