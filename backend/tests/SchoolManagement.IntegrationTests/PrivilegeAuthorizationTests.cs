using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolManagement.Api.Endpoints;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Domain.Security;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of the TASK-0002 authorisation substrate, against the reference
/// <c>GET /reference/arms/{armId}/secure</c> route (privilege <c>arm.view</c>, scoped by the
/// <c>armId</c> route parameter — spec 4.2.1 resolution rule 1).
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS TEST CLASS BUILDS ITS OWN <see cref="WebApplicationFactory{TEntryPoint}"/> RATHER THAN
/// USING THE SHARED <see cref="ApiTestFixture"/> CLIENT DIRECTLY: authentication (TASK-0003) is not
/// built yet, so there is no real way to become "an authenticated caller who holds X" through the
/// HTTP surface. This class substitutes a test-only authentication scheme (reads a
/// <c>X-Test-User-Id</c> header — never anything a real deployment would accept) and swaps
/// <see cref="IEffectivePrivilegeProvider"/>/<see cref="IAuthorizationAuditSink"/> for controllable
/// fakes. Everything else — routing, the authorization middleware, scope resolution, the
/// ProblemDetails response shaping — runs unmodified, against the real database-backed host.
/// </para>
/// <para>
/// Covers all four states the acceptance criteria name: anonymous, wrong-privilege, out-of-scope,
/// and in-scope, against the same route in each state.
/// </para>
/// </remarks>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class PrivilegeAuthorizationTests : IAsyncLifetime
{
    private const string UserIdHeader = "X-Test-User-Id";

    private readonly ApiTestFixture _fixture;

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private FakeEffectivePrivilegeProvider _grants = null!;
    private FakeAuthorizationAuditSink _auditSink = null!;

    public PrivilegeAuthorizationTests(ApiTestFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        if (!_fixture.IsDatabaseAvailable)
        {
            return;
        }

        await _fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        _grants = new FakeEffectivePrivilegeProvider();
        _auditSink = new FakeAuthorizationAuditSink();

        _factory = _fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    configureOptions: null);

            services.RemoveAll<IEffectivePrivilegeProvider>();
            services.AddSingleton<IEffectivePrivilegeProvider>(_grants);

            services.RemoveAll<IAuthorizationAuditSink>();
            services.AddSingleton<IAuthorizationAuditSink>(_auditSink);
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
    public async Task Anonymous_Returns401()
    {
        RequireDatabase();

        var armId = Guid.CreateVersion7();

        // No X-Test-User-Id header — the request carries no identity at all.
        var response = await _client.GetAsync(
            SecureArmUrl(armId),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WrongPrivilege_Returns403WithAGenericProblemBody()
    {
        RequireDatabase();

        var armId = Guid.CreateVersion7();
        const string userId = "user-wrong-privilege";

        // Holds a real, school-wide privilege — just not the one this route requires.
        _grants.SetGrants(userId, SchoolWideGrant(Privileges.Pupil.View));

        var response = await SendAsAsync(userId, armId);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await AssertGenericForbiddenBodyAsync(response);
        _auditSink.Rejections.ShouldContain(rejection =>
            rejection.UserId == userId && rejection.Privilege == Privileges.Arm.View);
    }

    [Fact]
    public async Task OutOfScope_Returns403WithAGenericProblemBody()
    {
        RequireDatabase();

        var targetArm = Guid.CreateVersion7();
        var otherArm = Guid.CreateVersion7();
        const string userId = "user-out-of-scope";

        // Holds arm.view — but scoped to a DIFFERENT arm than the one requested. Spec 4.2: "a
        // hand-crafted request naming [an arm outside the assignment] is rejected by the server with
        // 403 and no data leakage in the body."
        _grants.SetGrants(userId, ArmScopedGrant(Privileges.Arm.View, otherArm));

        var response = await SendAsAsync(userId, targetArm);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await AssertGenericForbiddenBodyAsync(response);
        _auditSink.Rejections.ShouldContain(rejection =>
            rejection.UserId == userId && rejection.Privilege == Privileges.Arm.View);
    }

    [Fact]
    public async Task InScope_ArmScoped_Returns200()
    {
        RequireDatabase();

        var armId = Guid.CreateVersion7();
        const string userId = "user-in-scope-arm";

        _grants.SetGrants(userId, ArmScopedGrant(Privileges.Arm.View, armId));

        var response = await SendAsAsync(userId, armId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SecureArmResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.ArmId.ShouldBe(armId);
    }

    [Fact]
    public async Task InScope_SchoolWide_Returns200()
    {
        RequireDatabase();

        var armId = Guid.CreateVersion7();
        const string userId = "user-in-scope-school-wide";

        // A school-wide grant covers every arm, per spec 4.2.1.
        _grants.SetGrants(userId, SchoolWideGrant(Privileges.Arm.View));

        var response = await SendAsAsync(userId, armId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NoGrantsAtAll_Returns403_NotAnEmptyResultOrTheEntity()
    {
        RequireDatabase();

        // An authenticated caller who holds nothing at all (the deny-by-default default while no
        // role/assignment module exists) — spec 9.2: object-level checks on a read must return 403,
        // never a filtered response and never the resource.
        var armId = Guid.CreateVersion7();
        const string userId = "user-no-grants";

        var response = await SendAsAsync(userId, armId);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private Task<HttpResponseMessage> SendAsAsync(string userId, Guid armId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, SecureArmUrl(armId));
        request.Headers.Add(UserIdHeader, userId);

        return _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Uri SecureArmUrl(Guid armId) =>
        new($"/api/v1/reference/arms/{armId}/secure", UriKind.Relative);

    private static async Task AssertGenericForbiddenBodyAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("errorCode").GetString().ShouldBe("authorization.forbidden");
        root.GetProperty("status").GetInt32().ShouldBe(403);

        // Spec 9.2: the body must contain "nothing but a generic message" — it must never name the
        // privilege that was checked or the arm that was requested.
        json.ShouldNotContain(Privileges.Arm.View);
    }

    private void RequireDatabase()
    {
        if (!_fixture.IsDatabaseAvailable)
        {
            Assert.Skip(_fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    private static PrivilegeGrant SchoolWideGrant(string privilege) =>
        new(privilege, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null);

    private static PrivilegeGrant ArmScopedGrant(string privilege, params Guid[] armIds) =>
        new(privilege, ScopeType.ArmList, armIds.ToHashSet(), SessionId: null);
}

/// <summary>
/// Test-only authentication scheme: authenticates as whatever user id the caller supplies in an
/// <c>X-Test-User-Id</c> header. Registered ONLY by <see cref="PrivilegeAuthorizationTests"/>'s
/// derived <see cref="WebApplicationFactory{TEntryPoint}"/> — it never ships, and never runs against
/// the production authentication scheme.
/// </summary>
internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User-Id", out var userId) ||
            string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: SchemeName);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>Controllable <see cref="IEffectivePrivilegeProvider"/> for exercising every decision branch.</summary>
internal sealed class FakeEffectivePrivilegeProvider : IEffectivePrivilegeProvider
{
    private readonly Dictionary<string, List<PrivilegeGrant>> _grantsByUser = new(StringComparer.Ordinal);

    public void SetGrants(string userId, params PrivilegeGrant[] grants) =>
        _grantsByUser[userId] = [.. grants];

    public Task<IReadOnlyCollection<PrivilegeGrant>> GetGrantsAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<PrivilegeGrant>>(
            _grantsByUser.TryGetValue(userId, out var grants) ? grants : []);
}

/// <summary>Records every rejection <see cref="PrivilegeAuthorizationTests"/> can assert against.</summary>
internal sealed class FakeAuthorizationAuditSink : IAuthorizationAuditSink
{
    public List<(string? UserId, string Privilege, string? RoutePath)> Rejections { get; } = [];

    public Task RecordRejectionAsync(
        string? userId,
        string privilege,
        string? routePath,
        CancellationToken cancellationToken)
    {
        Rejections.Add((userId, privilege, routePath));
        return Task.CompletedTask;
    }
}
