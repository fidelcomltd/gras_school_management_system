using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Domain.Security;

/// <summary>
/// Spec 4.2's escalation rule: "an attempt to create an arm-scoped assignment for a role
/// containing a non-scopable privilege is rejected at save time" with an exact message.
/// </summary>
public sealed class RoleScopeGuardTests
{
    [Fact]
    public void ArmScoped_WithOnlyScopablePrivileges_Succeeds()
    {
        var result = RoleScopeGuard.ValidateAssignable(
            [Privileges.Results.ScoreEnter, Privileges.Pupil.View, Privileges.Contact.View],
            ScopeType.ArmList);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void SchoolWide_WithNonScopablePrivileges_Succeeds()
    {
        // The rule only restricts arm-list scope. A school-wide assignment is exactly how a
        // non-scopable privilege like settings.grading.update is legitimately granted.
        var result = RoleScopeGuard.ValidateAssignable(
            [Privileges.Settings.GradingUpdate, Privileges.Results.ScoreEnter],
            ScopeType.SchoolWide);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ArmScoped_WithOneNonScopablePrivilege_FailsWithTheExactSpecMessage()
    {
        var result = RoleScopeGuard.ValidateAssignable(
            [Privileges.Settings.GradingUpdate],
            ScopeType.ArmList);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);

        // The literal sentence from spec 4.2. Do not loosen this assertion to a Contains — the exact
        // wording IS the acceptance criterion.
        result.Error.Description.ShouldBe(
            "This role contains privileges that cannot be limited to an arm: settings.grading.update. " +
            "Remove them from the role, or assign the role school-wide.");
    }

    [Fact]
    public void ArmScoped_WithSeveralNonScopablePrivileges_ListsAllOfThemSortedAndDeduplicated()
    {
        var result = RoleScopeGuard.ValidateAssignable(
            [Privileges.Settings.GradingUpdate, Privileges.Admin.Create, Privileges.Settings.GradingUpdate],
            ScopeType.ArmList);

        result.IsFailure.ShouldBeTrue();
        result.Error.Description.ShouldBe(
            "This role contains privileges that cannot be limited to an arm: admin.create, " +
            "settings.grading.update. Remove them from the role, or assign the role school-wide.");
    }

    [Fact]
    public void ArmScoped_ResolvesAGuardianAliasBeforeChecking()
    {
        // guardian.view aliases to contact.view, which IS scopable, so this must succeed rather than
        // being (wrongly) treated as an unknown, non-scopable code.
        var result = RoleScopeGuard.ValidateAssignable(["guardian.view"], ScopeType.ArmList);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidateAssignable_ThrowsForANullRolePrivilegesCollection()
    {
        Should.Throw<ArgumentNullException>(
            () => RoleScopeGuard.ValidateAssignable(null!, ScopeType.ArmList));
    }

    /// <summary>
    /// Spec 6.1.7 rule 3: "No account may grant a scope wider than its own." A school-wide actor
    /// scope has no width limit at all — this is how a School Administrator (who always holds
    /// role.scope.assign school-wide, since it is non-scopable — see <c>ValidateGrantWithinActorScope</c>'s
    /// own remarks) may assign Class Teacher over any arm.
    /// </summary>
    [Fact]
    public void GrantWithinActorScope_ActorIsSchoolWide_AnyRequestedScopeSucceeds()
    {
        var result = RoleScopeGuard.ValidateGrantWithinActorScope(
            actorScopeIsSchoolWide: true,
            actorArmIds: new HashSet<Guid>(),
            requestedScopeType: ScopeType.SchoolWide,
            requestedArmIds: [Guid.CreateVersion7()]);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void GrantWithinActorScope_ActorArmBoundRequestingSchoolWide_Fails()
    {
        var armId = Guid.CreateVersion7();

        var result = RoleScopeGuard.ValidateGrantWithinActorScope(
            actorScopeIsSchoolWide: false,
            actorArmIds: new HashSet<Guid> { armId },
            requestedScopeType: ScopeType.SchoolWide,
            requestedArmIds: []);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("role_assignment.scope_exceeds_actor");
    }

    [Fact]
    public void GrantWithinActorScope_RequestedArmsAreSubsetOfActorArms_Succeeds()
    {
        var armA = Guid.CreateVersion7();
        var armB = Guid.CreateVersion7();

        var result = RoleScopeGuard.ValidateGrantWithinActorScope(
            actorScopeIsSchoolWide: false,
            actorArmIds: new HashSet<Guid> { armA, armB },
            requestedScopeType: ScopeType.ArmList,
            requestedArmIds: [armA]);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void GrantWithinActorScope_RequestedArmOutsideActorArms_FailsNamingScopeExceeded()
    {
        var actorArm = Guid.CreateVersion7();
        var outsideArm = Guid.CreateVersion7();

        var result = RoleScopeGuard.ValidateGrantWithinActorScope(
            actorScopeIsSchoolWide: false,
            actorArmIds: new HashSet<Guid> { actorArm },
            requestedScopeType: ScopeType.ArmList,
            requestedArmIds: [actorArm, outsideArm]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role_assignment.scope_exceeds_actor");
    }

    [Fact]
    public void GrantWithinActorScope_ThrowsForNullActorArmIds()
    {
        Should.Throw<ArgumentNullException>(
            () => RoleScopeGuard.ValidateGrantWithinActorScope(false, null!, ScopeType.ArmList, []));
    }

    [Fact]
    public void GrantWithinActorScope_ThrowsForNullRequestedArmIds()
    {
        Should.Throw<ArgumentNullException>(
            () => RoleScopeGuard.ValidateGrantWithinActorScope(false, new HashSet<Guid>(), ScopeType.ArmList, null!));
    }
}
