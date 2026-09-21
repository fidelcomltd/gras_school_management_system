using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0086 stage B's remark-template endpoints (spec §6.7.7 delta item 4):
/// per-kind privilege enforced against a caller's REAL, controllable grant (not a Super Admin, who
/// would hold both kinds and could never prove the cross-kind refusals), the 403-before-404
/// ordering on DELETE, the duplicate/length rules, and creation order. Uses the same
/// <c>FakeEffectivePrivilegeProvider</c> swap <c>SectionEndpointsTests</c>/<c>TermEndpointsTests</c>
/// use, over a real cookie sign-in.
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class RemarkTemplateEndpointsTests : IAsyncLifetime
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string TemplatesUrl = "/api/v1/remark-templates";

    private readonly ApiTestFixture _fixture;

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private FakeEffectivePrivilegeProvider _grants = null!;
    private RecordingSystemAuditSink _auditSink = null!;

    public RemarkTemplateEndpointsTests(ApiTestFixture fixture) => _fixture = fixture;

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

    // ---- GET -----------------------------------------------------------------------------------

    [Fact]
    public async Task List_WithNoGrantAtAll_Returns403()
    {
        RequireDatabase();
        var jar = await SignInWithGrantsAsync();

        var response = await GetAsync($"{TemplatesUrl}?kind=ClassTeacher", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_ShipsEmpty()
    {
        RequireDatabase();
        var jar = await SignInWithGrantsAsync(ArmScoped(Privileges.Results.RemarkClassTeacher));

        var response = await GetAsync($"{TemplatesUrl}?kind=ClassTeacher", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<RemarkTemplateListDto>(response);
        body.Templates.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_WithAnArmScopedGrant_Succeeds()
    {
        // Ruling T: "the class-teacher list needs result.remark.classteacher at ANY scope, and
        // arm-scoped counts" — proven with a caller who is NOT school-wide.
        RequireDatabase();
        var jar = await SignInWithGrantsAsync(ArmScoped(Privileges.Results.RemarkClassTeacher));
        await CreateAsync(jar, RemarkKind.ClassTeacher, "A pleasure to have in school.");

        var response = await GetAsync($"{TemplatesUrl}?kind=ClassTeacher", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<RemarkTemplateListDto>(response);
        body.Templates.Single().Text.ShouldBe("A pleasure to have in school.");
    }

    [Fact]
    public async Task List_HoldingOnlyTheOtherKindsPrivilege_Returns403()
    {
        RequireDatabase();
        var jar = await SignInWithGrantsAsync(SchoolWide(Privileges.Results.RemarkHeadTeacher));

        var response = await GetAsync($"{TemplatesUrl}?kind=ClassTeacher", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_IsInCreationOrder()
    {
        RequireDatabase();
        var jar = await SignInWithGrantsAsync(SchoolWide(Privileges.Results.RemarkClassTeacher));
        await CreateAsync(jar, RemarkKind.ClassTeacher, "First phrase.");
        await CreateAsync(jar, RemarkKind.ClassTeacher, "Second phrase.");
        await CreateAsync(jar, RemarkKind.ClassTeacher, "Third phrase.");

        var response = await GetAsync($"{TemplatesUrl}?kind=ClassTeacher", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<RemarkTemplateListDto>(response);
        body.Templates.Select(template => template.Text).ShouldBe(["First phrase.", "Second phrase.", "Third phrase."]);
    }

    // ---- POST ----------------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithAnArmScopedGrant_Returns201WithTheShapeTheDeltaNames()
    {
        RequireDatabase();
        var jar = await SignInWithGrantsAsync(ArmScoped(Privileges.Results.RemarkClassTeacher));

        var response = await CreateAsync(jar, RemarkKind.ClassTeacher, "  A pleasure to have in school.  ");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadAsync<RemarkTemplateDto>(response);
        body.Kind.ShouldBe(RemarkKind.ClassTeacher);
        body.Text.ShouldBe("A pleasure to have in school.");
        body.Id.ShouldNotBeNullOrWhiteSpace();
        response.Headers.Location.ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_HoldingOnlyTheOtherKindsPrivilege_Returns403()
    {
        RequireDatabase();
        var jar = await SignInWithGrantsAsync(SchoolWide(Privileges.Results.RemarkHeadTeacher));

        var response = await CreateAsync(jar, RemarkKind.ClassTeacher, "Text");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_ADuplicateTrimmedCaseInsensitive_Returns409()
    {
        RequireDatabase();
        var jar = await SignInWithGrantsAsync(SchoolWide(Privileges.Results.RemarkClassTeacher));
        await CreateAsync(jar, RemarkKind.ClassTeacher, "A pleasure to have in school.");

        var response = await CreateAsync(jar, RemarkKind.ClassTeacher, "  A PLEASURE TO HAVE IN SCHOOL.  ");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe("remark_template.duplicate");
    }

    [Fact]
    public async Task Create_SameTextDifferentKind_BothSucceed()
    {
        // The two kinds' lists never collide with each other.
        RequireDatabase();
        var jar = await SignInWithGrantsAsync(
            SchoolWide(Privileges.Results.RemarkClassTeacher), SchoolWide(Privileges.Results.RemarkHeadTeacher));
        await CreateAsync(jar, RemarkKind.ClassTeacher, "Keep up the good work.");

        var response = await CreateAsync(jar, RemarkKind.HeadTeacher, "Keep up the good work.");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_TextOverThreeHundredCharacters_Returns422()
    {
        RequireDatabase();
        var jar = await SignInWithGrantsAsync(SchoolWide(Privileges.Results.RemarkClassTeacher));

        var response = await CreateAsync(jar, RemarkKind.ClassTeacher, new string('a', 301));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_WithoutIdempotencyKey_Returns400()
    {
        RequireDatabase();
        var jar = await SignInWithGrantsAsync(SchoolWide(Privileges.Results.RemarkClassTeacher));

        var response = await CreateAsync(jar, RemarkKind.ClassTeacher, "Text", idempotencyKey: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- DELETE ---------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_HoldingNeitherPrivilege_Returns403OnAnUnknownId()
    {
        RequireDatabase();
        var jar = await SignInWithGrantsAsync();

        var response = await DeleteAsync($"{TemplatesUrl}/{Guid.NewGuid()}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_HoldingAtLeastOnePrivilege_UnknownId_Returns404()
    {
        RequireDatabase();
        var jar = await SignInWithGrantsAsync(SchoolWide(Privileges.Results.RemarkClassTeacher));

        var response = await DeleteAsync($"{TemplatesUrl}/{Guid.NewGuid()}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_HoldingOnlyTheOtherKindsPrivilege_Returns403NeverDeletes()
    {
        RequireDatabase();
        var creatorJar = await SignInWithGrantsAsync(SchoolWide(Privileges.Results.RemarkClassTeacher));
        var created = await ReadAsync<RemarkTemplateDto>(await CreateAsync(creatorJar, RemarkKind.ClassTeacher, "Text"));

        var deleterJar = await SignInWithGrantsAsync(SchoolWide(Privileges.Results.RemarkHeadTeacher));
        var response = await DeleteAsync($"{TemplatesUrl}/{created.Id}", deleterJar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Set<RemarkTemplate>().AsNoTracking().AnyAsync(
            template => template.Id == Guid.Parse(created.Id), TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_WithTheMatchingKindsPrivilege_Returns204AndHardDeletes()
    {
        RequireDatabase();
        var jar = await SignInWithGrantsAsync(ArmScoped(Privileges.Results.RemarkClassTeacher));
        var created = await ReadAsync<RemarkTemplateDto>(await CreateAsync(jar, RemarkKind.ClassTeacher, "Text"));

        var response = await DeleteAsync($"{TemplatesUrl}/{created.Id}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Set<RemarkTemplate>().AsNoTracking().AnyAsync(
            template => template.Id == Guid.Parse(created.Id), TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    // ---- Grant-building and HTTP helpers ----------------------------------------------------------

    private static readonly Guid ArmId = Guid.CreateVersion7();

    private static PrivilegeGrant SchoolWide(string privilege) => new(privilege, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null);

    private static PrivilegeGrant ArmScoped(string privilege) => new(privilege, ScopeType.ArmList, new HashSet<Guid> { ArmId }, SessionId: null);

    private void RequireDatabase()
    {
        if (!_fixture.IsDatabaseAvailable)
        {
            Assert.Skip(_fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    private Task<HttpResponseMessage> CreateAsync(CookieJar jar, RemarkKind kind, string text, string? idempotencyKey = "set") =>
        PostAsync(
            TemplatesUrl, jar, new CreateRemarkTemplateCommand(kind, text),
            idempotencyKey == "set" ? $"key-{Guid.NewGuid():N}" : idempotencyKey);

    private async Task<CookieJar> SignInWithGrantsAsync(params PrivilegeGrant[] grants)
    {
        var (accountId, email, _) = await AdminAccountSeeder.SeedRegularAsync(_fixture);

        _grants.SetGrants(accountId.ToString("D", CultureInfo.InvariantCulture), grants);

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
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            },
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

    private Task<HttpResponseMessage> DeleteAsync(string url, CookieJar jar) => DeleteAsyncCore(_client, url, jar);

    private static async Task<HttpResponseMessage> DeleteAsyncCore(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
