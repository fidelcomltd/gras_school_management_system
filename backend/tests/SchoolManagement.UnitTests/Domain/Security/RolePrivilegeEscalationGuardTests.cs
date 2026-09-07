using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Domain.Security;

/// <summary>
/// Spec 6.1.7 rule 2: "No account may add a privilege to a role that it does not itself currently
/// hold." Approved delta wording pinned verbatim, per <c>RoleScopeGuardTests</c>' own precedent.
/// </summary>
public sealed class RolePrivilegeEscalationGuardTests
{
    [Fact]
    public void Create_ActorHoldsEveryRequestedPrivilege_Succeeds()
    {
        var result = RolePrivilegeEscalationGuard.ValidateAddition(
            existingPrivileges: [],
            requestedPrivileges: [Privileges.Pupil.View, Privileges.Results.ScoreEnter],
            actorPrivileges: [Privileges.Pupil.View, Privileges.Results.ScoreEnter, Privileges.Admin.View]);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_ActorMissingOneRequestedPrivilege_FailsWithTheExactSingularSpecMessage()
    {
        var result = RolePrivilegeEscalationGuard.ValidateAddition(
            existingPrivileges: [],
            requestedPrivileges: [Privileges.Role.Update, Privileges.Settings.GradingUpdate],
            actorPrivileges: [Privileges.Role.Update]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("role.privilege_escalation");

        // The literal sentence from the approved contract delta. Do not loosen this to a Contains —
        // the exact wording IS the acceptance criterion (mirrors the escalation guard's worked
        // example — "an account holding role.update but not settings.grading.update").
        result.Error.Description.ShouldBe(
            "You do not hold settings.grading.update and cannot add it to a role.");
    }

    [Fact]
    public void Create_ActorMissingSeveralRequestedPrivileges_ListsThemInRegisterOrder()
    {
        // Admin.View precedes Results.ScoreEnter in PrivilegeRegistry.All (4.4.1 before 4.4.5), so the
        // message must name them in THAT order, never alphabetical (which would reverse them).
        var result = RolePrivilegeEscalationGuard.ValidateAddition(
            existingPrivileges: [],
            requestedPrivileges: [Privileges.Results.ScoreEnter, Privileges.Admin.View],
            actorPrivileges: []);

        result.IsFailure.ShouldBeTrue();
        result.Error.Description.ShouldBe(
            "You do not hold admin.view, result.score.enter and cannot add them to a role.");
    }

    [Fact]
    public void Update_APrivilegeAlreadyOnTheRole_IsNeverTreatedAsAnAddition()
    {
        // The actor holds NOTHING, but every requested privilege is already on the role, so nothing
        // was actually "added".
        var result = RolePrivilegeEscalationGuard.ValidateAddition(
            existingPrivileges: [Privileges.Settings.GradingUpdate],
            requestedPrivileges: [Privileges.Settings.GradingUpdate],
            actorPrivileges: []);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Update_RemovingAPrivilegeTheActorDoesNotHold_IsUnrestricted()
    {
        // Narrowing (removal) is never the guard's concern — only additions are.
        var result = RolePrivilegeEscalationGuard.ValidateAddition(
            existingPrivileges: [Privileges.Settings.GradingUpdate, Privileges.Pupil.View],
            requestedPrivileges: [Privileges.Pupil.View],
            actorPrivileges: [Privileges.Pupil.View]);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Update_AddingAGenuinelyNewPrivilegeTheActorDoesNotHold_Fails()
    {
        var result = RolePrivilegeEscalationGuard.ValidateAddition(
            existingPrivileges: [Privileges.Pupil.View],
            requestedPrivileges: [Privileges.Pupil.View, Privileges.Settings.GradingUpdate],
            actorPrivileges: [Privileges.Pupil.View]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Description.ShouldBe(
            "You do not hold settings.grading.update and cannot add it to a role.");
    }

    [Fact]
    public void SuperAdmin_WideningARole_NeverTripsTheGuard_BecauseItAlreadyHoldsEverything()
    {
        // Spec 6.1.7: "a Super Admin can always widen a role, because a Super Admin holds
        // everything" — no special case in the guard, it just falls out of the general rule when
        // actorPrivileges is the whole register.
        var wholeRegister = PrivilegeRegistry.All.Select(definition => definition.Code).ToArray();

        var result = RolePrivilegeEscalationGuard.ValidateAddition(
            existingPrivileges: [],
            requestedPrivileges: wholeRegister,
            actorPrivileges: wholeRegister);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void DuplicateOffendingCodesInTheRequest_AreListedOnce()
    {
        var result = RolePrivilegeEscalationGuard.ValidateAddition(
            existingPrivileges: [],
            requestedPrivileges: [Privileges.Settings.GradingUpdate, Privileges.Settings.GradingUpdate],
            actorPrivileges: []);

        result.IsFailure.ShouldBeTrue();
        result.Error.Description.ShouldBe(
            "You do not hold settings.grading.update and cannot add it to a role.");
    }

    [Fact]
    public void ValidateAddition_ThrowsForANullExistingPrivilegesCollection()
    {
        Should.Throw<ArgumentNullException>(
            () => RolePrivilegeEscalationGuard.ValidateAddition(null!, [], []));
    }

    [Fact]
    public void ValidateAddition_ThrowsForANullRequestedPrivilegesCollection()
    {
        Should.Throw<ArgumentNullException>(
            () => RolePrivilegeEscalationGuard.ValidateAddition([], null!, []));
    }

    [Fact]
    public void ValidateAddition_ThrowsForANullActorPrivilegesCollection()
    {
        Should.Throw<ArgumentNullException>(
            () => RolePrivilegeEscalationGuard.ValidateAddition([], [], null!));
    }
}
