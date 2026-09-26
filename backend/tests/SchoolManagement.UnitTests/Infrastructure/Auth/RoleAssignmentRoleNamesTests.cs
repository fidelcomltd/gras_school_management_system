using NSubstitute;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Domain.Security;
using SchoolManagement.Infrastructure.Authorization;

namespace SchoolManagement.UnitTests.Infrastructure.Auth;

/// <summary>The header's role names: from the same active assignments the privileges come from, display only.</summary>
public sealed class RoleAssignmentRoleNamesTests
{
    private readonly IRoleAssignmentRepository _assignments = Substitute.For<IRoleAssignmentRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();

    [Fact]
    public async Task ASuperAdmin_IsShownAsSuperAdmin_WithoutReadingAssignments()
    {
        var names = await new RoleAssignmentRoleNames(_assignments, _roles)
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

        var names = await new RoleAssignmentRoleNames(_assignments, _roles)
            .GetActiveRoleNamesAsync(accountId, isSuperAdmin: false, TestContext.Current.CancellationToken);

        names.ShouldBe(["Bursar", "Class Teacher"]);
    }

    private static RoleAssignment Assignment(Guid accountId, Guid roleId) =>
        RoleAssignment.Create(Guid.NewGuid(), accountId, roleId, Guid.NewGuid(), ScopeType.SchoolWide, [], accountId).Value;
}
