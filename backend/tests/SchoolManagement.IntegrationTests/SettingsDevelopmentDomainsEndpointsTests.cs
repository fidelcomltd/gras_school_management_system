using System.Net;
using System.Net.Http.Json;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0072 stage 2b's endpoint against the approved contract delta: the four
/// seeded nursery development domains on a fresh database (spec 6.2.13 / Appendix E.3), the whole-set
/// atomic replace, optimistic concurrency, and <c>GET /settings</c> carrying the group.
/// </summary>
public sealed class SettingsDevelopmentDomainsEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string SettingsUrl = "/api/v1/settings";
    private const string DevelopmentDomainsUrl = "/api/v1/settings/development-domains";

    [Fact]
    public async Task GetSettings_OnAFreshDatabase_ReturnsTheFourSeededDevelopmentDomains()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();

        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));

        settings.DevelopmentDomains.VersionNumber.ShouldBe(0);
        settings.DevelopmentDomains.Domains.Count.ShouldBe(4);
        settings.DevelopmentDomains.Domains.Sum(domain => domain.Indicators.Count).ShouldBe(45);
        settings.DevelopmentDomains.Domains.ShouldAllBe(domain => domain.Section == "Nursery");
        settings.DevelopmentDomains.Domains.ShouldAllBe(domain => domain.Status == DevelopmentDomainStatus.Active);

        var mathsReadiness = settings.DevelopmentDomains.Domains.Single(domain => domain.Name == DevelopmentDomainSeed.MathsReadinessName);
        mathsReadiness.ActiveIndicatorCount.ShouldBe(4);
    }

    [Fact]
    public async Task UpdateDevelopmentDomains_WhileAnonymous_Returns401()
    {
        RequireDatabase();

        using var request = new HttpRequestMessage(HttpMethod.Put, DevelopmentDomainsUrl)
        {
            Content = JsonContent.Create(new UpdateDevelopmentDomainsCommand([], ExpectedVersion: 0, Reason: null)),
        };

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateDevelopmentDomains_HappyPath_RoundTripsAWholeSetReplace()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        var nurserySectionId = Guid.Parse(
            settings.DevelopmentDomains.Domains.Single(domain => domain.Name == DevelopmentDomainSeed.MathsReadinessName).SectionId);
        var ratingScaleId = Guid.Parse(
            settings.DevelopmentDomains.Domains.Single(domain => domain.Name == DevelopmentDomainSeed.MathsReadinessName).RatingScaleId);

        var command = new UpdateDevelopmentDomainsCommand(
            [new DevelopmentDomainInput(nurserySectionId, "Custom Domain", 1, ratingScaleId, true, DevelopmentDomainStatus.Active,
                [new DevelopmentIndicatorInput("First indicator", 1, DevelopmentIndicatorStatus.Active)])],
            ExpectedVersion: 0,
            Reason: null);

        var response = await PutAsync(DevelopmentDomainsUrl, jar, command);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await ReadAsync<SettingsDevelopmentDomainGroupDto>(response);
        result.VersionNumber.ShouldBe(1);
        result.Domains.Count.ShouldBe(1);
        result.Domains[0].Name.ShouldBe("Custom Domain");
        result.Domains[0].ActiveIndicatorCount.ShouldBe(1);

        var refetched = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        refetched.DevelopmentDomains.Domains.Count.ShouldBe(1);
        refetched.DevelopmentDomains.VersionNumber.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateDevelopmentDomains_WithAStaleExpectedVersion_Returns409()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        var command = ToWholeSetCommand(settings, expectedVersion: 0);

        var firstResponse = await PutAsync(DevelopmentDomainsUrl, jar, command);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondResponse = await PutAsync(DevelopmentDomainsUrl, jar, command);

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(secondResponse);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("settings.developmentdomains.stale_version");
    }

    [Fact]
    public async Task UpdateDevelopmentDomains_ArchiveThenResave_KeepsTheDomainIdAndPersistsTheStatus()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var settings = await ReadAsync<SettingsDto>(await GetAsync(SettingsUrl, jar));
        var mathsReadiness = settings.DevelopmentDomains.Domains.Single(d => d.Name == DevelopmentDomainSeed.MathsReadinessName);

        var archived = settings.DevelopmentDomains.Domains
            .Select(domain => domain.Name == DevelopmentDomainSeed.MathsReadinessName ? WithStatus(domain, DevelopmentDomainStatus.Archived) : domain)
            .ToList();
        var archiveCommand = new UpdateDevelopmentDomainsCommand(archived.Select(ToInput).ToList(), ExpectedVersion: 0, Reason: null);

        var archiveResponse = await PutAsync(DevelopmentDomainsUrl, jar, archiveCommand);
        archiveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterArchive = await ReadAsync<SettingsDevelopmentDomainGroupDto>(archiveResponse);
        var archivedDomain = afterArchive.Domains.Single(d => d.Id == mathsReadiness.Id);
        archivedDomain.Status.ShouldBe(DevelopmentDomainStatus.Archived);
        archivedDomain.ActiveIndicatorCount.ShouldBe(0);

        // Resave unchanged — the domain's id must survive both saves.
        var resaveCommand = new UpdateDevelopmentDomainsCommand(afterArchive.Domains.Select(ToInput).ToList(), ExpectedVersion: 1, Reason: null);
        var resaveResponse = await PutAsync(DevelopmentDomainsUrl, jar, resaveCommand);

        resaveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterResave = await ReadAsync<SettingsDevelopmentDomainGroupDto>(resaveResponse);
        afterResave.Domains.Single(d => d.Id == mathsReadiness.Id).Status.ShouldBe(DevelopmentDomainStatus.Archived);
    }

    private static UpdateDevelopmentDomainsCommand ToWholeSetCommand(SettingsDto settings, int expectedVersion) =>
        new(settings.DevelopmentDomains.Domains.Select(ToInput).ToList(), expectedVersion, null);

    private static DevelopmentDomainDto WithStatus(DevelopmentDomainDto domain, DevelopmentDomainStatus status) => domain with { Status = status };

    private static DevelopmentDomainInput ToInput(DevelopmentDomainDto domain) => new(
        Guid.Parse(domain.SectionId),
        domain.Name,
        domain.DisplayOrder,
        Guid.Parse(domain.RatingScaleId),
        domain.AllowsIndicatorComment,
        domain.Status,
        domain.Indicators.Select(ToInput).ToList(),
        Guid.Parse(domain.Id));

    private static DevelopmentIndicatorInput ToInput(DevelopmentIndicatorDto indicator) =>
        new(indicator.Name, indicator.DisplayOrder, indicator.Status, Guid.Parse(indicator.Id));

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
