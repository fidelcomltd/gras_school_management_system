using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Auth.AdminAccounts;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Security.Assignments;
using SchoolManagement.Domain.Audit;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0030's security core: the assignment entity's 6.1.5 shape (including the
/// arm-belongs-to-session check), escalation rules 1 and 3 with their audit events, the seeded Super
/// Admin role's assignment being refused, deactivation revoking assignments without reactivation
/// restoring them, and — the point of the whole card — <c>IEffectivePrivilegeProvider</c> resolving a
/// non-super-admin's grants from real <c>role_assignment</c> rows rather than a flag.
/// </summary>
/// <remarks>
/// Unlike <c>RoleEndpointsTests</c>, this class does NOT substitute
/// <c>IEffectivePrivilegeProvider</c> — the whole point is proving the REAL, DI-registered
/// <c>RoleAssignmentEffectivePrivilegeProvider</c> resolves a signed-in caller's grants from actual
/// database rows. A caller's privileges are established by seeding a real <c>role_assignment</c> row
/// directly through the DbContext (the same accepted technique <c>RoleEndpointsTests.CreateRoleDirectlyAsync</c>
/// uses) — including, for the rule-3 tests, an arm-scoped grant of <c>role.scope.assign</c> that the
/// LIVE endpoint could never produce (it is non-scopable — see <c>RoleScopeGuard.ValidateGrantWithinActorScope</c>'s
/// own remarks), so the guard's own logic is proven directly rather than left unreachable.
/// </remarks>
public sealed class AssignmentEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string AdminsUrl = "/api/v1/admins";
    private const string AssignmentsUrl = "/api/v1/assignments";

    [Fact]
    public async Task Create_SchoolWide_AsSuperAdmin_HappyPath_Returns201()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var (targetId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var roleId = await SeedRoleAsync("Report Reader", [Privileges.Report.View]);
        var sessionId = await SeedSessionAsync("2026/2027");

        var response = await PostAsync(
            $"{AdminsUrl}/{targetId}/assignments",
            jar,
            new CreateRoleAssignmentCommand(targetId.ToString(), roleId.ToString(), sessionId.ToString(), ScopeType.SchoolWide, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        var body = await ReadAsync<RoleAssignmentDto>(response);
        body.AdminAccountId.ShouldBe(targetId.ToString("D", CultureInfo.InvariantCulture));
        body.RoleId.ShouldBe(roleId.ToString("D", CultureInfo.InvariantCulture));
        body.ScopeType.ShouldBe(ScopeType.SchoolWide);
        body.ArmIds.ShouldBeEmpty();
        body.Status.ShouldBe(RoleAssignmentStatus.Active);
    }

    [Fact]
    public async Task Create_ArmScoped_AsSuperAdmin_HappyPath_Returns201()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var (targetId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var roleId = await SeedRoleAsync("Class Reader", [Privileges.Pupil.View]);
        var sessionId = await SeedSessionAsync("2026/2027");
        var armId = await SeedArmAsync(sessionId, "1A");

        var response = await PostAsync(
            $"{AdminsUrl}/{targetId}/assignments",
            jar,
            new CreateRoleAssignmentCommand(
                targetId.ToString(), roleId.ToString(), sessionId.ToString(), ScopeType.ArmList, [armId.ToString()]),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadAsync<RoleAssignmentDto>(response);
        body.ScopeType.ShouldBe(ScopeType.ArmList);
        body.ArmIds.ShouldBe([armId.ToString("D", CultureInfo.InvariantCulture)]);

        // TASK-0046: names travel with the ids, and the creation time is real, not a default.
        body.RoleName.ShouldBe("Class Reader");
        body.SessionName.ShouldBe("2026/2027");
        body.ArmNames.ShouldHaveSingleItem().ShouldEndWith("1A");
        body.CreatedAtUtc.ShouldNotBe(default);
    }

    [Fact]
    public async Task CopyToSession_DryRunWritesNothing_ThenCopiesWhatMapsAndSkipsTheRestWithReasons()
    {
        // TASK-0046 B, spec 4.2.2 / 6.1.14.
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var fromId = await SeedSessionAsync("2026/2027");
        var toId = await SeedSessionAsync("2027/2028");
        var from2A = await SeedArmAsync(fromId, "2A");
        var from5B = await SeedArmAsync(fromId, "5B");
        var to2A = await SeedArmAsync(toId, "2A");
        var (granterId, _) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var teacherRole = await SeedRoleAsync("Copy Teacher", [Privileges.Pupil.View]);
        var headRole = await SeedRoleAsync("Copy Head", [Privileges.Pupil.View]);

        var (mapsId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        await SeedAssignmentAsync(mapsId, teacherRole, fromId, ScopeType.ArmList, [from2A], granterId);
        var (unmatchedId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        await SeedAssignmentAsync(unmatchedId, teacherRole, fromId, ScopeType.ArmList, [from5B], granterId);
        var (headId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        await SeedAssignmentAsync(headId, headRole, fromId, ScopeType.SchoolWide, [], granterId);
        var (heldId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        await SeedAssignmentAsync(heldId, headRole, fromId, ScopeType.SchoolWide, [], granterId);
        await SeedAssignmentAsync(heldId, headRole, toId, ScopeType.SchoolWide, [], granterId);

        async Task<AssignmentCopyResultDto> CopyAsync(bool dryRun)
        {
            var response = await PostAsync(
                "/api/v1/assignments/copy-to-session",
                jar,
                new CopyAssignmentsToSessionCommand(fromId.ToString(), toId.ToString(), dryRun),
                idempotencyKey: $"key-{Guid.NewGuid():N}");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            return await ReadAsync<AssignmentCopyResultDto>(response);
        }

        string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

        var preview = await CopyAsync(dryRun: true);
        preview.Copied.Select(row => row.AdminAccountId).ShouldBe([Id(mapsId), Id(headId)], ignoreOrder: true);
        preview.Skipped.Single(row => row.AdminAccountId == Id(unmatchedId)).SkipReason!.ShouldContain("has no class in 2027/2028");
        preview.Skipped.Single(row => row.AdminAccountId == Id(heldId)).SkipReason.ShouldBe("Already assigned in 2027/2028.");
        (await ActiveInSessionAsync(toId)).ShouldBe(1, "a dry run writes nothing");

        var done = await CopyAsync(dryRun: false);
        done.Copied.Count.ShouldBe(2);
        (await ActiveInSessionAsync(toId)).ShouldBe(3);
        var mapped = await GetAsync($"{AdminsUrl}/{mapsId}/assignments", jar);
        var (toText, to2AText) = (Id(toId), Id(to2A));
        (await ReadAsync<List<RoleAssignmentDto>>(mapped)).ShouldContain(item =>
            item.SessionId == toText && item.ArmIds.SequenceEqual(new[] { to2AText }));

        (await CopyAsync(dryRun: true)).Copied.ShouldBeEmpty("copying twice finds everything already assigned");
    }

    // TASK-0046 C, spec 6.1.13.
    [Fact]
    public async Task Create_WithAnArchivedRole_Returns409()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var (targetId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var roleId = await SeedRoleAsync("Retired Role", [Privileges.Pupil.View]);
        var sessionId = await SeedSessionAsync("2026/2027");
        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await context.Roles.SingleAsync(role => role.Id == roleId, TestContext.Current.CancellationToken)).ChangeStatus(RoleStatus.Archived);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await PostAsync(
            $"{AdminsUrl}/{targetId}/assignments",
            jar,
            new CreateRoleAssignmentCommand(targetId.ToString(), roleId.ToString(), sessionId.ToString(), ScopeType.SchoolWide, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role_assignment.role_archived");
    }

    [Fact]
    public async Task Create_InAClosedSession_Returns409()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var (targetId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var roleId = await SeedRoleAsync("Late Role", [Privileges.Pupil.View]);
        var sessionId = await SeedSessionAsync("2025/2026");
        await CloseSessionAsync(sessionId);

        var response = await PostAsync(
            $"{AdminsUrl}/{targetId}/assignments",
            jar,
            new CreateRoleAssignmentCommand(targetId.ToString(), roleId.ToString(), sessionId.ToString(), ScopeType.SchoolWide, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role_assignment.session_closed");
        document.RootElement.GetProperty("detail").GetString().ShouldBe(
            "The session 2025/2026 is closed. Assignments can only be made in an open session.");
    }

    [Fact]
    public async Task DeletingAnArm_TakesItOffEveryAssignment_AndRevokesOneItEmpties()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var sessionId = await SeedSessionAsync("2026/2027");
        var doomed = await SeedArmAsync(sessionId, "2A");
        var kept = await SeedArmAsync(sessionId, "2B");
        var (granterId, _) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var roleId = await SeedRoleAsync("Class Teacher Too", [Privileges.Pupil.View]);
        var (teacherId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var both = await SeedAssignmentAsync(teacherId, roleId, sessionId, ScopeType.ArmList, [doomed, kept], granterId);
        var onlyDoomed = await SeedAssignmentAsync(teacherId, roleId, sessionId, ScopeType.ArmList, [doomed], granterId);

        using (var deleted = await DeleteAsync(Client, $"/api/v1/arms/{doomed}", jar))
        {
            deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var trimmed = await context.RoleAssignments.AsNoTracking().SingleAsync(a => a.Id == both, TestContext.Current.CancellationToken);
        trimmed.ArmIds.ShouldBe([kept]);
        trimmed.Status.ShouldBe(RoleAssignmentStatus.Active);
        var emptied = await context.RoleAssignments.AsNoTracking().SingleAsync(a => a.Id == onlyDoomed, TestContext.Current.CancellationToken);
        emptied.Status.ShouldBe(RoleAssignmentStatus.Revoked);
        (await context.AuditEvents.AsNoTracking().CountAsync(
            e => e.Action == "role_assignment.revoked_arm_deleted" && e.EntityId == onlyDoomed.ToString(),
            TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    private async Task CloseSessionAsync(Guid sessionId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE academic_sessions SET state = {SessionState.Closed.ToString()} WHERE id = {sessionId}",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CopyToSession_IntoAClosedSession_Returns409()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var fromId = await SeedSessionAsync("2026/2027");
        var closedId = await SeedSessionAsync("2025/2026");
        await CloseSessionAsync(closedId);

        var response = await PostAsync(
            "/api/v1/assignments/copy-to-session",
            jar,
            new CopyAssignmentsToSessionCommand(fromId.ToString(), closedId.ToString(), DryRun: true),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role_assignment.session_closed");
    }

    private async Task<int> ActiveInSessionAsync(Guid sessionId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.RoleAssignments.CountAsync(
            assignment => assignment.SessionId == sessionId && assignment.Status == RoleAssignmentStatus.Active,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ListAdmins_ShowsRolesHeldAndScopeSummary()
    {
        // TASK-0046, spec 6.1.8's "Roles held" and "Scope summary" columns.
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var sessionId = await SeedSessionAsync("2026/2027");
        var armA = await SeedArmAsync(sessionId, "2A");
        var armB = await SeedArmAsync(sessionId, "5B");
        var (granterId, _) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var teacherRole = await SeedRoleAsync("Class Teacher Copy", [Privileges.Pupil.View]);
        var headRole = await SeedRoleAsync("Head Copy", [Privileges.Pupil.View]);

        var (teacherId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        await SeedAssignmentAsync(teacherId, teacherRole, sessionId, ScopeType.ArmList, [armA, armB], granterId);
        var (headId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        await SeedAssignmentAsync(headId, headRole, sessionId, ScopeType.SchoolWide, [], granterId);
        await SeedAssignmentAsync(headId, teacherRole, sessionId, ScopeType.ArmList, [armA], granterId);
        var (idleId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);

        var response = await GetAsync($"{AdminsUrl}?pageSize=50", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = (await ReadAsync<CursorPage<AdminAccountSummaryDto>>(response)).Items;
        AdminAccountSummaryDto Row(Guid id) => items.Single(item => item.Id == id.ToString("D", CultureInfo.InvariantCulture));

        Row(teacherId).RolesHeld.ShouldBe(["Class Teacher Copy"]);
        Row(teacherId).ScopeSummary.ShouldStartWith("2 classes: ");
        Row(headId).RolesHeld.ShouldBe(["Class Teacher Copy", "Head Copy"]);
        Row(headId).ScopeSummary.ShouldBe("School-wide");
        Row(idleId).RolesHeld.ShouldBeEmpty();
        Row(idleId).ScopeSummary.ShouldBeEmpty();
        Row(granterId).RolesHeld.ShouldBe(["Super Admin"]);
    }

    [Fact]
    public async Task Create_ArmFromADifferentSession_Returns422NamingTheOffendingArm()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var (targetId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var roleId = await SeedRoleAsync("Class Reader Two", [Privileges.Pupil.View]);
        var sessionId = await SeedSessionAsync("2026/2027");
        var otherSessionId = await SeedSessionAsync("2027/2028");
        var armInOtherSession = await SeedArmAsync(otherSessionId, "2A");

        var response = await PostAsync(
            $"{AdminsUrl}/{targetId}/assignments",
            jar,
            new CreateRoleAssignmentCommand(
                targetId.ToString(), roleId.ToString(), sessionId.ToString(), ScopeType.ArmList,
                [armInOtherSession.ToString()]),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role_assignment.arm_not_in_session");
    }

    [Fact]
    public async Task Create_SuperAdminRole_IsRefused()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var (targetId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var sessionId = await SeedSessionAsync("2026/2027");

        var response = await PostAsync(
            $"{AdminsUrl}/{targetId}/assignments",
            jar,
            new CreateRoleAssignmentCommand(
                targetId.ToString(), SeededRoles.SuperAdminId.ToString(), sessionId.ToString(), ScopeType.SchoolWide, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role_assignment.super_admin_not_assignable");
    }

    [Fact]
    public async Task Create_RuleOne_SelfAssignment_Returns403AndWritesAnAuditEvent()
    {
        RequireDatabase();

        var auditSink = new RecordingSystemAuditSink();
        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemAuditSink>();
            services.AddSingleton<ISystemAuditSink>(auditSink);
        }));
        using var client = factory.CreateClient();

        var (actorId, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(client, CsrfUrl, jar);
        var signIn = await PostAsync(client, SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var roleId = await SeedRoleAsync("Self Assign Role", [Privileges.Pupil.View]);
        var sessionId = await SeedSessionAsync("2026/2027");

        var response = await PostAsync(
            client,
            $"{AdminsUrl}/{actorId}/assignments",
            jar,
            new CreateRoleAssignmentCommand(actorId.ToString(), roleId.ToString(), sessionId.ToString(), ScopeType.SchoolWide, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role_assignment.self_assignment_forbidden");
        document.RootElement.GetProperty("detail").GetString()
            .ShouldBe("You cannot change your own roles. Ask another Super Admin.");

        auditSink.Records.ShouldContain(record => record.Action == "role_assignment.self_assignment_forbidden");
    }

    [Fact]
    public async Task Create_RuleThree_ActorArmScopedNarrowerThanRequested_Returns403AndWritesAnAuditEvent()
    {
        RequireDatabase();

        var auditSink = new RecordingSystemAuditSink();
        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemAuditSink>();
            services.AddSingleton<ISystemAuditSink>(auditSink);
        }));
        using var client = factory.CreateClient();

        var sessionId = await SeedSessionAsync("2026/2027");
        var actorArm = await SeedArmAsync(sessionId, "3A");
        var outsideArm = await SeedArmAsync(sessionId, "3B");

        var (actorId, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var (granterId, _) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);

        // The actor's OWN reach: role.scope.assign, but arm-scoped to actorArm only — unreachable via
        // the live endpoint (role.scope.assign is non-scopable) but exactly what this guard exists to
        // catch once seeded directly, per the type's own remarks.
        var scopeAssignRoleId = await SeedRoleAsync("Narrow Scope Assigner", [Privileges.Role.ScopeAssign]);
        await SeedAssignmentAsync(actorId, scopeAssignRoleId, sessionId, ScopeType.ArmList, [actorArm], granterId);

        var jar = new CookieJar();
        await GetAsync(client, CsrfUrl, jar);
        var signIn = await PostAsync(client, SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (targetId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var grantedRoleId = await SeedRoleAsync("Class Reader Three", [Privileges.Pupil.View]);

        var response = await PostAsync(
            client,
            $"{AdminsUrl}/{targetId}/assignments",
            jar,
            new CreateRoleAssignmentCommand(
                targetId.ToString(), grantedRoleId.ToString(), sessionId.ToString(), ScopeType.ArmList,
                [outsideArm.ToString()]),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role_assignment.scope_exceeds_actor");

        auditSink.Records.ShouldContain(record => record.Action == "role_assignment.scope_exceeds_actor");
    }

    /// <summary>
    /// TASK-0048's own criterion, proven against the REAL, DI-registered <c>ISystemAuditSink</c> —
    /// no <c>RecordingSystemAuditSink</c> substitute, unlike every other test in this class.
    /// Queries Postgres directly, AFTER the command has already returned its 403, so a
    /// same-transaction implementation (which <c>UnitOfWork.ExecuteAtomicallyAsync</c> would have
    /// rolled back) cannot pass this test by accident.
    /// </summary>
    [Fact]
    public async Task Create_RuleOne_SelfAssignment_PersistsADurableRejectedAuditEventRow()
    {
        RequireDatabase();

        var (actorId, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var roleId = await SeedRoleAsync("Self Assign Durable Role", [Privileges.Pupil.View]);
        var sessionId = await SeedSessionAsync("2026/2027");

        var response = await PostAsync(
            $"{AdminsUrl}/{actorId}/assignments",
            jar,
            new CreateRoleAssignmentCommand(actorId.ToString(), roleId.ToString(), sessionId.ToString(), ScopeType.SchoolWide, null),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var persisted = await context.AuditEvents
            .AsNoTracking()
            .Where(e => e.Action == "role_assignment.self_assignment_forbidden" && e.ActorAdminId == actorId)
            .ToListAsync(TestContext.Current.CancellationToken);

        persisted.ShouldHaveSingleItem();
        persisted[0].Outcome.ShouldBe(AuditOutcome.Rejected);
    }

    /// <summary>Same proof as the rule-1 test above, for escalation rule 3.</summary>
    [Fact]
    public async Task Create_RuleThree_ActorArmScopedNarrowerThanRequested_PersistsADurableRejectedAuditEventRow()
    {
        RequireDatabase();

        var sessionId = await SeedSessionAsync("2026/2027");
        var actorArm = await SeedArmAsync(sessionId, "3C");
        var outsideArm = await SeedArmAsync(sessionId, "3D");

        var (actorId, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var (granterId, _) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);

        var scopeAssignRoleId = await SeedRoleAsync("Narrow Scope Assigner Durable", [Privileges.Role.ScopeAssign]);
        await SeedAssignmentAsync(actorId, scopeAssignRoleId, sessionId, ScopeType.ArmList, [actorArm], granterId);

        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (targetId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var grantedRoleId = await SeedRoleAsync("Class Reader Durable", [Privileges.Pupil.View]);

        var response = await PostAsync(
            $"{AdminsUrl}/{targetId}/assignments",
            jar,
            new CreateRoleAssignmentCommand(
                targetId.ToString(), grantedRoleId.ToString(), sessionId.ToString(), ScopeType.ArmList,
                [outsideArm.ToString()]),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var persisted = await context.AuditEvents
            .AsNoTracking()
            .Where(e => e.Action == "role_assignment.scope_exceeds_actor" && e.ActorAdminId == actorId)
            .ToListAsync(TestContext.Current.CancellationToken);

        persisted.ShouldHaveSingleItem();
        persisted[0].Outcome.ShouldBe(AuditOutcome.Rejected);
    }

    [Fact]
    public async Task Create_RuleThree_RequestedArmWithinActorsOwnScope_Succeeds()
    {
        RequireDatabase();

        var sessionId = await SeedSessionAsync("2026/2027");
        var actorArmA = await SeedArmAsync(sessionId, "4A");
        var actorArmB = await SeedArmAsync(sessionId, "4B");

        var (actorId, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var (granterId, _) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);

        var scopeAssignRoleId = await SeedRoleAsync("Wide Enough Scope Assigner", [Privileges.Role.ScopeAssign]);
        await SeedAssignmentAsync(
            actorId, scopeAssignRoleId, sessionId, ScopeType.ArmList, [actorArmA, actorArmB], granterId);

        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (targetId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var grantedRoleId = await SeedRoleAsync("Class Reader Four", [Privileges.Pupil.View]);

        var response = await PostAsync(
            $"{AdminsUrl}/{targetId}/assignments",
            jar,
            new CreateRoleAssignmentCommand(
                targetId.ToString(), grantedRoleId.ToString(), sessionId.ToString(), ScopeType.ArmList,
                [actorArmA.ToString()]),
            idempotencyKey: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Revoke_RuleOne_SelfRevoke_Returns403AndWritesAnAuditEvent()
    {
        RequireDatabase();

        var auditSink = new RecordingSystemAuditSink();
        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemAuditSink>();
            services.AddSingleton<ISystemAuditSink>(auditSink);
        }));
        using var client = factory.CreateClient();

        var sessionId = await SeedSessionAsync("2026/2027");
        var (actorId, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var (granterId, _) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);

        var assignRoleId = await SeedRoleAsync("Own Role Assigner", [Privileges.Role.Assign]);
        await SeedAssignmentAsync(actorId, assignRoleId, sessionId, ScopeType.SchoolWide, [], granterId);

        var ownAssignmentId = await SeedAssignmentAsync(
            actorId, await SeedRoleAsync("Something Else", [Privileges.Pupil.View]), sessionId,
            ScopeType.SchoolWide, [], granterId);

        var jar = new CookieJar();
        await GetAsync(client, CsrfUrl, jar);
        var signIn = await PostAsync(client, SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await DeleteAsync(client, $"{AssignmentsUrl}/{ownAssignmentId}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("role_assignment.self_assignment_forbidden");

        auditSink.Records.ShouldContain(record => record.Action == "role_assignment.self_assignment_forbidden");
    }

    [Fact]
    public async Task Revoke_AsSuperAdmin_HappyPath_Returns204AndMarksRevoked()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var sessionId = await SeedSessionAsync("2026/2027");
        var (targetId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var (granterId, _) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var roleId = await SeedRoleAsync("Revocable Role", [Privileges.Pupil.View]);
        var assignmentId = await SeedAssignmentAsync(targetId, roleId, sessionId, ScopeType.SchoolWide, [], granterId);

        var response = await DeleteAsync($"{AssignmentsUrl}/{assignmentId}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await context.RoleAssignments.AsNoTracking()
            .SingleAsync(a => a.Id == assignmentId, TestContext.Current.CancellationToken);
        stored.Status.ShouldBe(RoleAssignmentStatus.Revoked);
    }

    [Fact]
    public async Task List_ReturnsEveryAssignmentForTheAccount_ActiveAndRevoked()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var sessionId = await SeedSessionAsync("2026/2027");
        var (targetId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var (granterId, _) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var roleId = await SeedRoleAsync("Listed Role", [Privileges.Pupil.View]);

        var activeId = await SeedAssignmentAsync(targetId, roleId, sessionId, ScopeType.SchoolWide, [], granterId);
        var revokedId = await SeedAssignmentAsync(targetId, roleId, sessionId, ScopeType.SchoolWide, [], granterId);
        await RevokeDirectlyAsync(revokedId);

        var response = await GetAsync($"{AdminsUrl}/{targetId}/assignments", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<List<RoleAssignmentDto>>(response);
        body.ShouldContain(item => item.Id == activeId.ToString("D", CultureInfo.InvariantCulture) && item.Status == RoleAssignmentStatus.Active
            && item.RoleName == "Listed Role" && item.SessionName == "2026/2027" && item.ArmNames.Count == 0);
        body.ShouldContain(item => item.Id == revokedId.ToString("D", CultureInfo.InvariantCulture) && item.Status == RoleAssignmentStatus.Revoked);
    }

    [Fact]
    public async Task Deactivation_RevokesActiveAssignments_AndReactivationDoesNotRestoreThem()
    {
        RequireDatabase();

        var jar = await SignInAsSuperAdminAsync();
        var sessionId = await SeedSessionAsync("2026/2027");
        var (targetId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var (granterId, _) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var roleId = await SeedRoleAsync("Deactivation Role", [Privileges.Pupil.View]);
        var assignmentId = await SeedAssignmentAsync(targetId, roleId, sessionId, ScopeType.SchoolWide, [], granterId);

        var deactivate = await PostAsync(
            $"{AdminsUrl}/{targetId}/status",
            jar,
            new ChangeAdminAccountStatusCommand(targetId, AdminAccountStatus.Deactivated, "Left the school."));
        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertAssignmentStatusAsync(assignmentId, RoleAssignmentStatus.Revoked);

        var reactivate = await PostAsync(
            $"{AdminsUrl}/{targetId}/status",
            jar,
            new ChangeAdminAccountStatusCommand(targetId, AdminAccountStatus.Active, null));
        reactivate.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Spec 6.1.10: reactivation does NOT restore revoked assignments.
        await AssertAssignmentStatusAsync(assignmentId, RoleAssignmentStatus.Revoked);
    }

    [Fact]
    public async Task Provider_NonSuperAdminWithoutAnAssignment_HoldsNoPrivileges()
    {
        RequireDatabase();

        var (_, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await GetAsync("/api/v1/roles", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Provider_NonSuperAdminWithARealAssignment_ResolvesThePrivilegeItGrants()
    {
        RequireDatabase();

        var sessionId = await SeedSessionAsync("2026/2027");
        var (accountId, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);
        var (granterId, _) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var roleId = await SeedRoleAsync("Role Viewer", [Privileges.Role.View]);
        await SeedAssignmentAsync(accountId, roleId, sessionId, ScopeType.SchoolWide, [], granterId);

        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await GetAsync("/api/v1/roles", jar);

        // role.view is exactly (and only) what the seeded assignment grants — this is the graduated
        // provider resolving a real row, not a flag and not a fixture substitute.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task AssertAssignmentStatusAsync(Guid assignmentId, RoleAssignmentStatus expected)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await context.RoleAssignments.AsNoTracking()
            .SingleAsync(a => a.Id == assignmentId, TestContext.Current.CancellationToken);
        stored.Status.ShouldBe(expected);
    }

    private async Task RevokeDirectlyAsync(Guid assignmentId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var assignment = await context.RoleAssignments.SingleAsync(
            a => a.Id == assignmentId, TestContext.Current.CancellationToken);
        assignment.Revoke();
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SeedRoleAsync(string name, IReadOnlyCollection<string> privileges)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var creation = Role.Create(Guid.CreateVersion7(), name, null, privileges);
        creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

        context.Add(creation.Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return creation.Value.Id;
    }

    private async Task<Guid> SeedSessionAsync(string name)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var existing = await context.AcademicSessions.AsNoTracking()
            .FirstOrDefaultAsync(session => session.Name == name, TestContext.Current.CancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        var startYear = int.Parse(name[..4], CultureInfo.InvariantCulture);
        var session = AcademicSession.Create(
            Guid.CreateVersion7(), name, new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25)).Value;

        context.Add(session);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return session.Id;
    }

    private async Task<Guid> SeedArmAsync(Guid sessionId, string label)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(TestContext.Current.CancellationToken)).Id;

        var arm = Arm.Create(Guid.CreateVersion7(), levelId, sessionId, label, null, null).Value;

        context.Add(arm);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return arm.Id;
    }

    private async Task<Guid> SeedAssignmentAsync(
        Guid adminAccountId,
        Guid roleId,
        Guid sessionId,
        ScopeType scopeType,
        IReadOnlyCollection<Guid> armIds,
        Guid grantedBy)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Seeded directly, bypassing RoleScopeGuard/rule 3 — this is TEST SETUP establishing the
        // caller's own pre-existing reach, the same accepted technique other test files use to reach
        // otherwise-unreachable prerequisite state (see this class's own remarks).
        var creation = RoleAssignment.Create(
            Guid.CreateVersion7(), adminAccountId, roleId, sessionId, scopeType, armIds, grantedBy);
        creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

        context.Add(creation.Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return creation.Value.Id;
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

    private Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload, string? idempotencyKey = null) =>
        PostAsync(Client, url, jar, payload, idempotencyKey);

    private static async Task<HttpResponseMessage> PostAsync<T>(
        HttpClient client, string url, CookieJar jar, T payload, string? idempotencyKey = null)
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

    private Task<HttpResponseMessage> DeleteAsync(string url, CookieJar jar) => DeleteAsync(Client, url, jar);

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
