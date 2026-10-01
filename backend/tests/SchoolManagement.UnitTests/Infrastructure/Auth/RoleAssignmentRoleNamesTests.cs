using NSubstitute;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Authorization;

namespace SchoolManagement.UnitTests.Infrastructure.Auth;

/// <summary>The header's role names: from the same active assignments the privileges come from, display only.</summary>
public sealed class RoleAssignmentRoleNamesTests
{
    private readonly IRoleAssignmentRepository _assignments = Substitute.For<IRoleAssignmentRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IAcademicSessionRepository _sessions = Substitute.For<IAcademicSessionRepository>();

    [Fact]
    public async Task ASuperAdmin_IsShownAsSuperAdmin_WithoutReadingAssignments()
    {
        var names = await new RoleAssignmentRoleNames(new ActiveRoleAssignmentLoader(_assignments, _roles, _sessions))
            .GetActiveRoleNamesAsync(Guid.NewGuid(), isSuperAdmin: true, TestContext.Current.CancellationToken);

        names.ShouldBe([RoleAssignmentRoleNames.SuperAdminName]);
        _assignments.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task AnyoneElse_GetsEachActiveRoleOnce_ByName_SkippingARoleThatNoLongerResolves()
    {
        var accountId = Guid.NewGuid();
        var teacher = Role.Create(Guid.NewGuid(), "Class Teacher", null, [Privileges.Pupil.View]).Value;
        var bursar = Role.Create(Guid.NewGuid(), "Bursar", null, [Privileges.Pupil.View]).Value;
        var missingRoleId = Guid.NewGuid();
        _assignments.ListActiveForAccountReadOnlyAsync(accountId, Arg.Any<CancellationToken>()).Returns(
        [
            Assignment(accountId, teacher.Id), Assignment(accountId, teacher.Id), Assignment(accountId, bursar.Id),
            Assignment(accountId, missingRoleId),
        ]);
        _roles.FindReadOnlyByIdAsync(teacher.Id, Arg.Any<CancellationToken>()).Returns(teacher);
        _roles.FindReadOnlyByIdAsync(bursar.Id, Arg.Any<CancellationToken>()).Returns(bursar);

        var names = await new RoleAssignmentRoleNames(new ActiveRoleAssignmentLoader(_assignments, _roles, _sessions))
            .GetActiveRoleNamesAsync(accountId, isSuperAdmin: false, TestContext.Current.CancellationToken);

        names.ShouldBe(["Bursar", "Class Teacher"]);
    }

    [Fact]
    public async Task TheLoader_ReadsAnAccountsAssignmentsOncePerScope_HoweverManyTimesItIsAsked()
    {
        var accountId = Guid.NewGuid();
        var teacher = Role.Create(Guid.NewGuid(), "Class Teacher", null, [Privileges.Pupil.View]).Value;
        _assignments.ListActiveForAccountReadOnlyAsync(accountId, Arg.Any<CancellationToken>())
            .Returns([Assignment(accountId, teacher.Id)]);
        _roles.FindReadOnlyByIdAsync(teacher.Id, Arg.Any<CancellationToken>()).Returns(teacher);
        var loader = new ActiveRoleAssignmentLoader(_assignments, _roles, _sessions);

        await loader.LoadAsync(accountId, TestContext.Current.CancellationToken);
        var again = await loader.LoadAsync(accountId, TestContext.Current.CancellationToken);

        again.Count.ShouldBe(1);
        await _assignments.Received(1).ListActiveForAccountReadOnlyAsync(accountId, Arg.Any<CancellationToken>());
        await _roles.Received(1).FindReadOnlyByIdAsync(teacher.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnlyTheActiveSessionsAssignmentsCount_OrEveryOneBeforeAnySessionIsActive()
    {
        // TASK-0046 B, ruled 2026-09-28 (spec 4.2.2): last session's assignments do not carry over.
        var accountId = Guid.NewGuid();
        var teacher = Role.Create(Guid.NewGuid(), "Class Teacher", null, [Privileges.Pupil.View]).Value;
        var bursar = Role.Create(Guid.NewGuid(), "Bursar", null, [Privileges.Pupil.View]).Value;
        var active = AcademicSession.Create(Guid.NewGuid(), "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 25)).Value;
        _assignments.ListActiveForAccountReadOnlyAsync(accountId, Arg.Any<CancellationToken>()).Returns(
            [Assignment(accountId, teacher.Id, active.Id), Assignment(accountId, bursar.Id, Guid.NewGuid())]);
        _roles.FindReadOnlyByIdAsync(teacher.Id, Arg.Any<CancellationToken>()).Returns(teacher);
        _roles.FindReadOnlyByIdAsync(bursar.Id, Arg.Any<CancellationToken>()).Returns(bursar);

        var beforeAnySession = await new RoleAssignmentRoleNames(new ActiveRoleAssignmentLoader(_assignments, _roles, _sessions))
            .GetActiveRoleNamesAsync(accountId, isSuperAdmin: false, TestContext.Current.CancellationToken);
        _sessions.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(active);
        var loader = new ActiveRoleAssignmentLoader(_assignments, _roles, _sessions);
        var now = await new RoleAssignmentRoleNames(loader)
            .GetActiveRoleNamesAsync(accountId, isSuperAdmin: false, TestContext.Current.CancellationToken);

        beforeAnySession.ShouldBe(["Bursar", "Class Teacher"]);
        now.ShouldBe(["Class Teacher"]);
        (await loader.LoadAsync(accountId, TestContext.Current.CancellationToken)).Count.ShouldBe(2, "every session's, for target-session checks");
    }

    private static RoleAssignment Assignment(Guid accountId, Guid roleId, Guid sessionId) =>
        RoleAssignment.Create(Guid.NewGuid(), accountId, roleId, sessionId, ScopeType.SchoolWide, [], accountId).Value;

    private static RoleAssignment Assignment(Guid accountId, Guid roleId) =>
        RoleAssignment.Create(Guid.NewGuid(), accountId, roleId, Guid.NewGuid(), ScopeType.SchoolWide, [], accountId).Value;
}
