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
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Security.Roles;
using SchoolManagement.Domain.Security;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0028 dispatch 2's five endpoints against the approved contract delta
/// (`.agent/decisions/2026-Q3-contract-deltas.md`, entry `TASK-0028`, §2-4): reserved-name and
/// duplicate-name rejection, unknown-privilege naming the offender, the system-role 409, hard delete,
/// the default-scope archive filter, and spec 6.1.7 rule 2 end-to-end with its audit event.
/// </summary>
/// <remarks>
/// <para>
/// Builds its OWN <see cref="WebApplicationFactory{TEntryPoint}"/> substituting
/// <see cref="IEffectivePrivilegeProvider"/> for a controllable fake — same technique as
/// <c>PrivilegeAuthorizationTests</c>, but layered UNDER the real cookie-session authentication
/// (unlike that class's test-only scheme), because these routes need genuine CSRF/session mechanics.
/// Today's flag-only <c>SuperAdminFlagEffectivePrivilegeProvider</c> can only produce "holds
/// everything" or "holds nothing," so rule 2 (an account holding <c>role.update</c> but not
/// <c>settings.grading.update</c>) cannot be proven with a real signed-in caller otherwise — the
/// exact gap the task card's live drift entry names, left for TASK-0030's provider graduation.
/// </para>
/// <para>
/// TASK-0028 dispatch 3 seeded the real six roles into every test's database (spec 4.5,
/// <c>ApiTestFixture.ReseedRolesAsync</c>), so the system-role 409 is now proven against the ACTUAL
/// seeded <c>SeededRoles.SuperAdminId</c> row rather than a fixture-created stand-in — the
/// fixture-based <c>CreateSystemRoleDirectlyAsync</c> helper and its two tests were removed, since a
/// second row also named "Super Admin" cannot coexist under the name-uniqueness index, and every
/// arbitrary test role name elsewhere in this file that collided with a real seeded name ("Class
/// Teacher") was renamed to something that cannot.
/// </para>
/// </remarks>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class RoleEndpointsTests : IAsyncLifetime
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string RolesUrl = "/api/v1/roles";

    private readonly ApiTestFixture _fixture;

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private FakeEffectivePrivilegeProvider _grants = null!;
    private RecordingSystemAuditSink _auditSink = null!;

    public RoleEndpointsTests(ApiTestFixture fixture) => _fixture = fixture;

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
    public async Task Create_WithRoleCreatePrivilege_HappyPath_Returns201WithLocationAndBody()
    {
        RequireDatabase();

        // Rule 2 (spec 6.1.7): the actor must hold every privilege it is granting to the new role, so
        // this "happy path" caller needs pupil.view too, not just role.create.
        var jar = await SignInWithGrantsAsync(Privileges.Role.Create, Privileges.Pupil.View);
        var command = new CreateRoleCommand("Custom Marking Role", "Marks and pupil records.", [Privileges.Pupil.View]);

        var response = await PostAsync(RolesUrl, jar, command, idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldStartWith($"{RolesUrl}/");

        var body = await ReadAsync<RoleDto>(response);
        body.Name.ShouldBe("Custom Marking Role");
        body.Description.ShouldBe("Marks and pupil records.");
        body.IsSystem.ShouldBeFalse();
        body.Status.ShouldBe(RoleStatus.Active);
        body.Privileges.ShouldBe([Privileges.Pupil.View]);
    }

    [Fact]
    public async Task Create_WithoutAnIdempotencyKey_Returns400KeyMissing()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Role.Create);

        using var request = new HttpRequestMessage(HttpMethod.Post, RolesUrl)
        {
            Content = JsonContent.Create(
                new CreateRoleCommand("Custom Marking Role", null, [Privileges.Pupil.View])),
        };
        jar.ApplyWithCsrf(request);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("idempotency.key_missing");
    }

    [Fact]
    public async Task Create_WithoutCsrfToken_Returns403()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Role.Create);

        using var request = new HttpRequestMessage(HttpMethod.Post, RolesUrl)
        {
            Content = JsonContent.Create(new CreateRoleCommand("Custom Marking Role", null, [Privileges.Pupil.View])),
        };
        jar.Apply(request); // cookies only, no X-CSRF-Token
        request.Headers.Add("Idempotency-Key", $"key-{Guid.NewGuid():N}");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Super Admin")]
    [InlineData("super admin")]
    public async Task Create_WithTheReservedNameCaseInsensitive_Returns409BecauseTheRealRowAlreadyExists(
        string reservedName)
    {
        RequireDatabase();

        // Before TASK-0028 dispatch 3 seeded the real Super Admin row, no role named "Super Admin"
        // existed anywhere, so CreateRoleCommandHandler's duplicate-name check (which runs BEFORE
        // Role.Create's reserved-name check — see the handler's own comment) found nothing and the
        // reserved-name rejection (422 role.name_reserved) was the one actually observed over HTTP.
        // Now a real row occupies that name permanently, so the duplicate check wins first: 409, not
        // 422. The reserved-name rule itself is unaffected and still fully proven independent of any
        // database state — RoleTests.Create_WithTheReservedNameCaseInsensitive_Rejects — this test only
        // pins what a real caller actually observes in production.
        var jar = await SignInWithGrantsAsync(Privileges.Role.Create);
        var command = new CreateRoleCommand(reservedName, null, [Privileges.Pupil.View]);

        var response = await PostAsync(RolesUrl, jar, command, idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role.name_duplicate");
    }

    [Fact]
    public async Task Create_DuplicateNameCaseInsensitive_Returns409()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Role.Create, Privileges.Pupil.View);
        var name = $"Duplicate Role {Guid.NewGuid():N}";

        var first = await PostAsync(
            RolesUrl, jar, new CreateRoleCommand(name, null, [Privileges.Pupil.View]), $"key-{Guid.NewGuid():N}");
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await PostAsync(
            RolesUrl,
            jar,
            new CreateRoleCommand(name.ToUpperInvariant(), null, [Privileges.Pupil.View]),
            $"key-{Guid.NewGuid():N}");

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(second);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role.name_duplicate");
    }

    [Fact]
    public async Task Create_WithAnUnknownPrivilegeCode_Returns422NamingTheOffender()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Role.Create);
        var command = new CreateRoleCommand("Custom Marking Role", null, ["not.a.real.code"]);

        var response = await PostAsync(RolesUrl, jar, command, $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role.unknown_privilege");
        document.RootElement.GetProperty("detail").GetString()
            .ShouldBe("'not.a.real.code' is not a recognised privilege code.");
    }

    [Fact]
    public async Task Create_WithoutRoleCreatePrivilege_Returns403()
    {
        RequireDatabase();

        // Signed in, holding SOME privilege, just not role.create.
        var jar = await SignInWithGrantsAsync(Privileges.Role.View);
        var command = new CreateRoleCommand("Custom Marking Role", null, [Privileges.Pupil.View]);

        var response = await PostAsync(RolesUrl, jar, command, $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_RuleTwo_EscalationAttempt_Returns403AndWritesAnAuditEvent()
    {
        RequireDatabase();

        // Holds role.create AND pupil.view, but NOT settings.grading.update — the escalation guard's
        // own worked example (spec 6.1.7 rule 2). pupil.view is granted too so it is not ALSO flagged
        // as an offender, keeping the rejection message pinned to the one code this test is about.
        var jar = await SignInWithGrantsAsync(Privileges.Role.Create, Privileges.Pupil.View);
        var command = new CreateRoleCommand(
            "Grading Overreach", null, [Privileges.Pupil.View, Privileges.Settings.GradingUpdate]);

        var response = await PostAsync(RolesUrl, jar, command, $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role.privilege_escalation");
        document.RootElement.GetProperty("detail").GetString().ShouldBe(
            "You do not hold settings.grading.update and cannot add it to a role.");

        _auditSink.Records.ShouldContain(record => record.Action == "role.privilege_escalation");

        // Nothing was actually created.
        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM roles WHERE name = 'Grading Overreach'")
            .SingleAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(0);
    }

    [Fact]
    public async Task List_DefaultExcludesArchivedRoles_ButStatusFilterIncludesThemExplicitly()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Role.View, Privileges.Role.Create, Privileges.Role.Update);
        var roleId = await CreateRoleDirectlyAsync("Archived Role", [Privileges.Pupil.View], RoleStatus.Archived);

        var defaultList = await ReadAsync<CursorPage<RoleDto>>(await GetAsync(RolesUrl, jar));
        defaultList.Items.ShouldNotContain(item => item.Id == roleId.ToString("D", CultureInfo.InvariantCulture));

        var explicitList = await ReadAsync<CursorPage<RoleDto>>(
            await GetAsync($"{RolesUrl}?status=Archived", jar));
        explicitList.Items.ShouldContain(item => item.Id == roleId.ToString("D", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Get_WhenTheRoleDoesNotExist_Returns404()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Role.View);

        var response = await GetAsync($"{RolesUrl}/{Guid.CreateVersion7()}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role.not_found");
    }

    [Fact]
    public async Task Update_RenameOnly_LeavesPrivilegesAndStatusUnchanged()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Role.Update, Privileges.Role.Create);
        var roleId = await CreateRoleDirectlyAsync("Original Name", [Privileges.Pupil.View]);

        var response = await PatchAsync(
            $"{RolesUrl}/{roleId}", jar, new UpdateRoleCommand(roleId, "Renamed", null, null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<RoleDto>(response);
        body.Name.ShouldBe("Renamed");
        body.Privileges.ShouldBe([Privileges.Pupil.View]);
        body.Status.ShouldBe(RoleStatus.Active);
    }

    [Fact]
    public async Task Update_DescriptionSetToAnEmptyString_ClearsIt()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Role.Update, Privileges.Role.Create);
        var roleId = await CreateRoleDirectlyAsync("Has A Description", [Privileges.Pupil.View], description: "Some text.");

        var response = await PatchAsync(
            $"{RolesUrl}/{roleId}", jar, new UpdateRoleCommand(roleId, null, string.Empty, null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<RoleDto>(response);
        body.Description.ShouldBeNull();
    }

    [Fact]
    public async Task Update_DuplicateName_Returns409()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Role.Update, Privileges.Role.Create);
        var takenName = $"Taken {Guid.NewGuid():N}";
        await CreateRoleDirectlyAsync(takenName, [Privileges.Pupil.View]);
        var roleId = await CreateRoleDirectlyAsync($"Other {Guid.NewGuid():N}", [Privileges.Pupil.View]);

        var response = await PatchAsync(
            $"{RolesUrl}/{roleId}", jar, new UpdateRoleCommand(roleId, takenName, null, null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role.name_duplicate");
    }

    [Fact]
    public async Task Update_RemovingAPrivilegeTheActorDoesNotHold_IsUnrestrictedByRuleTwo()
    {
        RequireDatabase();

        // Holds ONLY role.update — narrowing the role never trips the escalation guard.
        var jar = await SignInWithGrantsAsync(Privileges.Role.Update, Privileges.Role.Create);
        var roleId = await CreateRoleDirectlyAsync(
            "Narrowing Role", [Privileges.Pupil.View, Privileges.Settings.GradingUpdate]);

        var response = await PatchAsync(
            $"{RolesUrl}/{roleId}", jar, new UpdateRoleCommand(roleId, null, null, [Privileges.Pupil.View], null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<RoleDto>(response);
        body.Privileges.ShouldBe([Privileges.Pupil.View]);
    }

    [Fact]
    public async Task Update_RuleTwo_AddingAPrivilegeTheActorDoesNotHold_Returns403AndWritesAnAuditEvent()
    {
        RequireDatabase();

        // Holds role.update and role.view (to read the role back afterward) but NOT
        // settings.grading.update.
        var jar = await SignInWithGrantsAsync(Privileges.Role.Update, Privileges.Role.View);
        var roleId = await CreateRoleDirectlyAsync("Escalation Target", [Privileges.Pupil.View]);

        var response = await PatchAsync(
            $"{RolesUrl}/{roleId}",
            jar,
            new UpdateRoleCommand(
                roleId, null, null, [Privileges.Pupil.View, Privileges.Settings.GradingUpdate], null));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role.privilege_escalation");

        _auditSink.Records.ShouldContain(record =>
            record.Action == "role.privilege_escalation" && record.EntityId == roleId.ToString());

        // The rejected mutation must not have been persisted — rolled back with the whole request.
        var stored = await ReadAsync<RoleDto>(await GetAsync($"{RolesUrl}/{roleId}", jar));
        stored.Privileges.ShouldBe([Privileges.Pupil.View]);
    }

    [Fact]
    public async Task Update_OnTheRealSeededSuperAdminRole_Returns409()
    {
        RequireDatabase();

        // TASK-0028 dispatch 3: unlike every other 409 test in this class, this one names NO
        // fixture-created system role — SeededRoles.SuperAdminId is the actual row spec 4.5 ships in
        // production (reinserted by ApiTestFixture.ReseedRolesAsync after every TRUNCATE), so this is
        // the first time the 409 the task card's own live-drift entry named ("finally reachable in
        // production rather than only through a test fixture") is proven against it directly.
        var jar = await SignInWithGrantsAsync(Privileges.Role.Update);

        var response = await PatchAsync(
            $"{RolesUrl}/{SeededRoles.SuperAdminId}",
            jar,
            new UpdateRoleCommand(SeededRoles.SuperAdminId, "New Name", null, null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role.system_immutable");
    }

    [Fact]
    public async Task Delete_HappyPath_Returns204AndRemovesTheRow()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Role.Delete, Privileges.Role.Create, Privileges.Role.View);
        var roleId = await CreateRoleDirectlyAsync("Deletable Role", [Privileges.Pupil.View]);

        var response = await DeleteAsync($"{RolesUrl}/{roleId}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getAfter = await GetAsync($"{RolesUrl}/{roleId}", jar);
        getAfter.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WhenTheRoleDoesNotExist_Returns404()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Role.Delete);

        var response = await DeleteAsync($"{RolesUrl}/{Guid.CreateVersion7()}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_OnTheRealSeededSuperAdminRole_Returns409()
    {
        RequireDatabase();

        // Same reasoning as Update_OnTheRealSeededSuperAdminRole_Returns409 — this must return 409,
        // not the 204/404 an actual delete against a live production row would otherwise produce.
        var jar = await SignInWithGrantsAsync(Privileges.Role.Delete, Privileges.Role.View);

        var response = await DeleteAsync($"{RolesUrl}/{SeededRoles.SuperAdminId}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role.system_immutable");

        // Must still exist, unchanged — nothing about a rejected delete may remove or narrow the row.
        var stored = await ReadAsync<RoleDto>(await GetAsync($"{RolesUrl}/{SeededRoles.SuperAdminId}", jar));
        stored.IsSystem.ShouldBeTrue();
        stored.Privileges.Count.ShouldBe(93);
    }

    [Fact]
    public async Task Delete_WithoutRoleDeletePrivilege_Returns403()
    {
        RequireDatabase();

        var jar = await SignInWithGrantsAsync(Privileges.Role.Create, Privileges.Role.View);
        var roleId = await CreateRoleDirectlyAsync("Protected Role", [Privileges.Pupil.View]);

        var response = await DeleteAsync($"{RolesUrl}/{roleId}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private void RequireDatabase()
    {
        if (!_fixture.IsDatabaseAvailable)
        {
            Assert.Skip(_fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    /// <summary>
    /// Seeds a regular admin account, signs in for real (cookie + CSRF), and grants it exactly
    /// <paramref name="privileges"/> school-wide through the substituted
    /// <see cref="IEffectivePrivilegeProvider"/> — the only way a real signed-in caller can hold a
    /// PARTIAL privilege set under today's flag-only provider.
    /// </summary>
    private async Task<CookieJar> SignInWithGrantsAsync(params string[] privileges)
    {
        var (accountId, email, _) = await AdminAccountSeeder.SeedRegularAsync(_fixture);

        _grants.SetGrants(
            accountId.ToString("D", CultureInfo.InvariantCulture),
            privileges.Select(privilege => new PrivilegeGrant(privilege, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null))
                .ToArray());

        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private async Task<Guid> CreateRoleDirectlyAsync(
        string name,
        IReadOnlyCollection<string> privileges,
        RoleStatus status = RoleStatus.Active,
        string? description = null)
    {
        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var creation = Role.Create(Guid.CreateVersion7(), name, description, privileges);
        creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

        var role = creation.Value;

        if (status != RoleStatus.Active)
        {
            role.ChangeStatus(status);
        }

        context.Add(role);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return role.Id;
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
