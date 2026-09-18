using System.Net;
using System.Net.Http.Json;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0072 stage 3b's endpoint against the approved contract delta: the 19
/// seeded traits on a fresh database (spec 6.2.7 / Appendix F.3), both blocks pointing at the Primary
/// trait scale, the whole-set atomic replace, optimistic concurrency, and <c>GET /settings</c> carrying
/// the group.
/// </summary>
public sealed class SettingsTraitsEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string SettingsUrl = "/api/v1/settings";
    private const string TraitsUrl = "/api/v1/settings/traits";

    [Fact]
    public async Task GetSettings_OnAFreshDatabase_ReturnsTheNineteenSeededTraits()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));

        settings.Traits.VersionNumber.ShouldBe(0);
        settings.Traits.Traits.Count.ShouldBe(19);
        settings.Traits.Traits.Count(trait => trait.Domain == TraitDomain.Affective).ShouldBe(11);
        settings.Traits.Traits.Count(trait => trait.Domain == TraitDomain.Psychomotor).ShouldBe(8);
        settings.Traits.Traits.ShouldAllBe(trait => trait.Status == TraitStatus.Active);

        // Both blocks point at the same scale — the seeded Primary trait scale (spec 6.2.13).
        settings.Traits.AffectiveRatingScaleId.ShouldBe(settings.Traits.PsychomotorRatingScaleId);

        var conduct = settings.Traits.Traits.Single(trait => trait.Name == "Conduct");
        conduct.Domain.ShouldBe(TraitDomain.Affective);
        conduct.DisplayOrder.ShouldBe(1);

        var sports = settings.Traits.Traits.Single(trait => trait.Name == "Sports");
        sports.Domain.ShouldBe(TraitDomain.Psychomotor);
        sports.DisplayOrder.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateTraits_WhileAnonymous_Returns401()
    {
        RequireDatabase();

        using var request = new HttpRequestMessage(HttpMethod.Put, TraitsUrl)
        {
            Content = JsonContent.Create(new UpdateTraitsCommand(Guid.NewGuid(), Guid.NewGuid(), [], ExpectedVersion: 0, Reason: null)),
        };

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateTraits_HappyPath_RoundTripsAWholeSetReplace()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        var affectiveScaleId = Guid.Parse(settings.Traits.AffectiveRatingScaleId);
        var psychomotorScaleId = Guid.Parse(settings.Traits.PsychomotorRatingScaleId);

        var command = new UpdateTraitsCommand(
            affectiveScaleId,
            psychomotorScaleId,
            [new TraitInput(TraitDomain.Affective, "Custom Trait", 1, TraitStatus.Active)],
            ExpectedVersion: 0,
            Reason: null);

        var response = await PutAsync(TraitsUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await ReadAsync<SettingsTraitsGroupDto>(response);
        result.VersionNumber.ShouldBe(1);
        result.Traits.Count.ShouldBe(1);
        result.Traits[0].Name.ShouldBe("Custom Trait");

        var refetched = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        refetched.Traits.Traits.Count.ShouldBe(1);
        refetched.Traits.VersionNumber.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateTraits_WithAStaleExpectedVersion_Returns409()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        var command = ToWholeSetCommand(settings, expectedVersion: 0);

        var firstResponse = await PutAsync(TraitsUrl, jar, command);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondResponse = await PutAsync(TraitsUrl, jar, command);

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(secondResponse);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.traits.stale_version");
    }

    [Fact]
    public async Task UpdateTraits_ArchiveThenResave_KeepsTheTraitIdAndPersistsTheStatus()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        var conduct = settings.Traits.Traits.Single(t => t.Name == "Conduct");

        var archived = settings.Traits.Traits
            .Select(trait => trait.Name == "Conduct" ? trait with { Status = TraitStatus.Archived } : trait)
            .ToList();
        var archiveCommand = new UpdateTraitsCommand(
            Guid.Parse(settings.Traits.AffectiveRatingScaleId),
            Guid.Parse(settings.Traits.PsychomotorRatingScaleId),
            archived.Select(ToInput).ToList(),
            ExpectedVersion: 0,
            Reason: null);

        var archiveResponse = await PutAsync(TraitsUrl, jar, archiveCommand);
        archiveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterArchive = await ReadAsync<SettingsTraitsGroupDto>(archiveResponse);
        var archivedTrait = afterArchive.Traits.Single(t => t.Id == conduct.Id);
        archivedTrait.Status.ShouldBe(TraitStatus.Archived);

        // Resave unchanged — the trait's id must survive both saves.
        var resaveCommand = new UpdateTraitsCommand(
            Guid.Parse(settings.Traits.AffectiveRatingScaleId),
            Guid.Parse(settings.Traits.PsychomotorRatingScaleId),
            afterArchive.Traits.Select(ToInput).ToList(),
            ExpectedVersion: 1,
            Reason: null);
        var resaveResponse = await PutAsync(TraitsUrl, jar, resaveCommand);

        resaveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterResave = await ReadAsync<SettingsTraitsGroupDto>(resaveResponse);
        afterResave.Traits.Single(t => t.Id == conduct.Id).Status.ShouldBe(TraitStatus.Archived);
    }

    private static UpdateTraitsCommand ToWholeSetCommand(SettingsDto settings, int expectedVersion) =>
        new(
            Guid.Parse(settings.Traits.AffectiveRatingScaleId),
            Guid.Parse(settings.Traits.PsychomotorRatingScaleId),
            settings.Traits.Traits.Select(ToInput).ToList(),
            expectedVersion,
            null);

    private static TraitInput ToInput(TraitDto trait) =>
        new(trait.Domain, trait.Name, trait.DisplayOrder, trait.Status, Guid.Parse(trait.Id));

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
