using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Classes;
using SchoolManagement.Domain.Security;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0038's section endpoints (spec 6.4.2, 6.4.9): the two seeded sections,
/// create, duplicate-name rejection, rename. Gated under <c>level.*</c> privileges, per the ruling
/// naming a section a property of a level rather than inventing <c>section.*</c> register rows.
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class SectionEndpointsTests : IAsyncLifetime
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string SectionsUrl = "/api/v1/sections";

    private readonly ApiTestFixture _fixture;

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private FakeEffectivePrivilegeProvider _grants = null!;
    private RecordingSystemAuditSink _auditSink = null!;

    public SectionEndpointsTests(ApiTestFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        if (!_fixture.IsDatabaseAvailable)
        {
            return;
        }

        await _fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        _grants = new FakeEffectivePrivilegeProvider();
        _auditSink = new RecordingSystemAuditSink();

        _factory = _fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEffectivePrivilegeProvider>();
            services.AddSingleton<IEffectivePrivilegeProvider>(_grants);

            services.RemoveAll<ISystemAuditSink>();
            services.AddSingleton<ISystemAuditSink>(_auditSink);
        }));

        _client = _factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);

        return _factory?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    [Fact]
    public async Task List_ReturnsTheTwoSeededSections()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.View);

        var response = await GetAsync(SectionsUrl, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<SectionListResponse>(response);
        body.Sections.Select(section => section.Name).ShouldBe(["Nursery", "Primary"]);

        // TASK-0083 ruling R1: Primary rates traits, Nursery does not.
        body.Sections.Single(section => section.Name == "Nursery").RatesTraits.ShouldBeFalse();
        body.Sections.Single(section => section.Name == "Primary").RatesTraits.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WithLevelCreatePrivilege_Succeeds()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Create);

        var response = await PostAsync(
            SectionsUrl, jar, new CreateSectionCommand("Secondary"), idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadAsync<SectionDto>(response);
        body.Name.ShouldBe("Secondary");
        body.RatesTraits.ShouldBeFalse();
    }

    [Fact]
    public async Task Create_WithRatesTraitsTrue_PersistsIt()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Create);

        var response = await PostAsync(
            SectionsUrl, jar, new CreateSectionCommand("Sixth Form", RatesTraits: true), idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadAsync<SectionDto>(response);
        body.RatesTraits.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_WithRatesTraitsOmitted_LeavesItUnchanged()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Create, Privileges.Level.Update, Privileges.Level.View);
        var created = await ReadAsync<SectionDto>(
            await PostAsync(SectionsUrl, jar, new CreateSectionCommand("Secondary", RatesTraits: true), idempotencyKey: $"key-{Guid.NewGuid():N}"));

        var response = await PatchAsync($"{SectionsUrl}/{created.Id}", jar, new { name = "Renamed Secondary" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<SectionDto>(response);
        body.RatesTraits.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_WithRatesTraitsExplicit_ChangesIt()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Create, Privileges.Level.Update, Privileges.Level.View);
        var created = await ReadAsync<SectionDto>(
            await PostAsync(SectionsUrl, jar, new CreateSectionCommand("Secondary"), idempotencyKey: $"key-{Guid.NewGuid():N}"));
        created.RatesTraits.ShouldBeFalse();

        var response = await PatchAsync($"{SectionsUrl}/{created.Id}", jar, new { name = "Secondary", ratesTraits = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<SectionDto>(response);
        body.RatesTraits.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WithDuplicateNameCaseInsensitive_Returns409()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Level.Create);

        var response = await PostAsync(
            SectionsUrl, jar, new CreateSectionCommand("NURSERY"), idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("section.name_duplicate");
    }

    [Fact]
    public async Task Update_RenamesTheSection()
    {
        RequireDatabase();

        var createJar = await SignInWithGrantsAsync(Privileges.Level.Create, Privileges.Level.Update, Privileges.Level.View);

        var created = await ReadAsync<SectionDto>(
            await PostAsync(SectionsUrl, createJar, new CreateSectionCommand("Secondary"), idempotencyKey: $"key-{Guid.NewGuid():N}"));

        var response = await PatchAsync($"{SectionsUrl}/{created.Id}", createJar, new { name = "Sixth Form" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<SectionDto>(response);
        body.Name.ShouldBe("Sixth Form");
    }

    private void RequireDatabase()
    {
        if (!_fixture.IsDatabaseAvailable)
        {
            Assert.Skip(_fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    private async Task<CookieJar> SignInWithGrantsAsync(params string[] privileges)
    {
        var (accountId, email, _) = await AdminAccountSeeder.SeedRegularAsync(_fixture);

        _grants.SetGrants(
            accountId.ToString("D", System.Globalization.CultureInfo.InvariantCulture),
            privileges.Select(privilege => new PrivilegeGrant(privilege, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null))
                .ToArray());

        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        where T : class
    {
        var value = await response.Content.ReadFromJsonAsync<T>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            TestContext.Current.CancellationToken);

        value.ShouldNotBeNull($"Expected a {typeof(T).Name} body but the response was empty. Status: {response.StatusCode}.");
        return value;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(json);
    }

    private Task<HttpResponseMessage> GetAsync(string url, CookieJar jar) => GetAsyncCore(_client, url, jar);

    private static async Task<HttpResponseMessage> GetAsyncCore(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload, string? idempotencyKey = null) =>
        PostAsyncCore(_client, url, jar, payload, idempotencyKey);

    private static async Task<HttpResponseMessage> PostAsyncCore<T>(
        HttpClient client, string url, CookieJar jar, T payload, string? idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PatchAsync<T>(string url, CookieJar jar, T payload) =>
        PatchAsyncCore(_client, url, jar, payload);

    private static async Task<HttpResponseMessage> PatchAsyncCore<T>(HttpClient client, string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(payload),
        };

        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
