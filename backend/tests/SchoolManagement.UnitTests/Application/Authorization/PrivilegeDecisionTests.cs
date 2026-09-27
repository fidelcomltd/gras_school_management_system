using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Authorization;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Application.Authorization;

/// <summary>
/// The pure decision spec 4.2.1 describes: "does the account hold the required privilege through at
/// least one active assignment whose scope is school-wide, or whose arm list contains the resolved
/// arm."
/// </summary>
public sealed class PrivilegeDecisionTests
{
    private static readonly Guid ArmA = Guid.CreateVersion7();
    private static readonly Guid ArmB = Guid.CreateVersion7();
    private static readonly Guid ThisSession = Guid.CreateVersion7();
    private static readonly Guid LastSession = Guid.CreateVersion7();

    [Fact]
    public void SchoolWideGrantInAnotherSession_DoesNotAuthorizeAResolvedArm()
    {
        // TASK-0060, spec 4.2.1: "in the session the target belongs to".
        var grants = new[] { SchoolWideGrant(Privileges.Results.View) with { SessionId = LastSession } };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.View, new ScopeResolution.ResolvedArm(ArmA, ThisSession))
            .ShouldBeFalse();
        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.View, new ScopeResolution.ResolvedArm(ArmA, LastSession))
            .ShouldBeTrue("last session's grant still covers last session's arms");
    }

    [Fact]
    public void RequiresSchoolWideInASession_CountsOnlyGrantsInThatSession()
    {
        // A pupil with no open enrolment belongs to the active session (TASK-0060).
        var grants = new[] { SchoolWideGrant(Privileges.Pupil.View) with { SessionId = LastSession } };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Pupil.View, new ScopeResolution.RequiresSchoolWide(ThisSession))
            .ShouldBeFalse();
        PrivilegeDecision.IsAuthorized(grants, Privileges.Pupil.View, new ScopeResolution.RequiresSchoolWide(LastSession))
            .ShouldBeTrue();
    }

    [Fact]
    public void SessionlessGrant_AuthorizesAResolvedArmInAnySession()
    {
        // Spec 4.2.2: the Super Admin assignment is sessionless and permanent.
        var grants = new[] { SchoolWideGrant(Privileges.Results.View) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.View, new ScopeResolution.ResolvedArm(ArmA, LastSession))
            .ShouldBeTrue();
    }

    [Fact]
    public void AnArmWithNoKnownSession_IsNotFilteredBySession()
    {
        // An unknown arm resolves with no session, so a school-wide holder reaches the handler's 404, not a 403.
        var grants = new[] { SchoolWideGrant(Privileges.Results.View) with { SessionId = LastSession } };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.View, new ScopeResolution.ResolvedArm(ArmA, null))
            .ShouldBeTrue();
    }

    [Fact]
    public void NoGrantsAtAll_IsNeverAuthorized()
    {
        var authorized = PrivilegeDecision.IsAuthorized(
            [], Privileges.Results.View, new ScopeResolution.ResolvedArm(ArmA, ThisSession));

        authorized.ShouldBeFalse();
    }

    [Fact]
    public void AGrantForADifferentPrivilege_DoesNotAuthorize()
    {
        var grants = new[] { SchoolWideGrant(Privileges.Pupil.View) };

        var authorized = PrivilegeDecision.IsAuthorized(
            grants, Privileges.Results.View, new ScopeResolution.ResolvedArm(ArmA, ThisSession));

        authorized.ShouldBeFalse();
    }

    [Fact]
    public void SchoolWideGrant_AuthorizesAnyResolvedArm()
    {
        var grants = new[] { SchoolWideGrant(Privileges.Results.View) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.View, new ScopeResolution.ResolvedArm(ArmA, ThisSession))
            .ShouldBeTrue();
        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.View, new ScopeResolution.ResolvedArm(ArmB, ThisSession))
            .ShouldBeTrue();
    }

    [Fact]
    public void SchoolWideGrant_AuthorizesRequiresSchoolWideAndNotApplicable()
    {
        var grants = new[] { SchoolWideGrant(Privileges.Settings.GradingUpdate) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Settings.GradingUpdate, new ScopeResolution.RequiresSchoolWide(SessionId: null))
            .ShouldBeTrue();
        PrivilegeDecision.IsAuthorized(grants, Privileges.Settings.GradingUpdate, new ScopeResolution.NotApplicable())
            .ShouldBeTrue();
    }

    [Fact]
    public void ArmScopedGrant_AuthorizesOnlyTheArmItLists()
    {
        var grants = new[] { ArmScopedGrant(Privileges.Results.ScoreEnter, ArmA) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.ScoreEnter, new ScopeResolution.ResolvedArm(ArmA, ThisSession))
            .ShouldBeTrue("the grant lists this arm");

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.ScoreEnter, new ScopeResolution.ResolvedArm(ArmB, ThisSession))
            .ShouldBeFalse("the grant does not list this arm — this is the out-of-scope case");
    }

    [Fact]
    public void ArmScopedGrant_NeverSatisfiesARequiresSchoolWideOutcome()
    {
        // The rule from spec 4.2.1 stated explicitly: "An arm-scoped holder cannot perform
        // level-wide operations even over a level containing only their own arm."
        var grants = new[] { ArmScopedGrant(Privileges.Promotion.Run, ArmA) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Promotion.Run, new ScopeResolution.RequiresSchoolWide(SessionId: null))
            .ShouldBeFalse();
    }

    [Fact]
    public void ArmScopedGrant_NeverSatisfiesNotApplicable()
    {
        var grants = new[] { ArmScopedGrant(Privileges.Results.ScoreEnter, ArmA) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.ScoreEnter, new ScopeResolution.NotApplicable())
            .ShouldBeFalse();
    }

    [Fact]
    public void UnresolvableTarget_IsNeverAuthorized_EvenWithASchoolWideGrant()
    {
        var grants = new[] { SchoolWideGrant(Privileges.Results.View) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.View, new ScopeResolution.Unresolvable())
            .ShouldBeFalse("an unresolvable target must fail closed, not fall back to 'any grant will do'");
    }

    [Fact]
    public void AGuardianAliasPrivilege_MatchesAGrantStoredUnderItsCanonicalContactName()
    {
        var grants = new[] { SchoolWideGrant(Privileges.Contact.View) };

        PrivilegeDecision.IsAuthorized(grants, "guardian.view", new ScopeResolution.NotApplicable())
            .ShouldBeTrue();
    }

    // ---- AnyGrant (TASK-0086 stage B) -----------------------------------------------------------

    [Fact]
    public void AnyGrant_AnArmScopedGrant_IsAuthorized()
    {
        var grants = new[] { ArmScopedGrant(Privileges.Results.RemarkClassTeacher, ArmA) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.RemarkClassTeacher, new ScopeResolution.AnyGrant())
            .ShouldBeTrue("an arm-scoped grant must pass AnyGrant — the whole point of this mode over NotApplicable");
    }

    [Fact]
    public void AnyGrant_ASchoolWideGrant_IsAuthorized()
    {
        var grants = new[] { SchoolWideGrant(Privileges.Results.RemarkHeadTeacher) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.RemarkHeadTeacher, new ScopeResolution.AnyGrant())
            .ShouldBeTrue();
    }

    [Fact]
    public void AnyGrant_NoGrantAtAll_Returns403()
    {
        PrivilegeDecision.IsAuthorized([], Privileges.Results.RemarkClassTeacher, new ScopeResolution.AnyGrant())
            .ShouldBeFalse();
    }

    [Fact]
    public void AnyGrant_AGrantForADifferentPrivilege_DoesNotAuthorize()
    {
        var grants = new[] { SchoolWideGrant(Privileges.Results.RemarkClassTeacher) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.RemarkHeadTeacher, new ScopeResolution.AnyGrant())
            .ShouldBeFalse("holding the OTHER kind's privilege must not satisfy this one");
    }

    private static PrivilegeGrant SchoolWideGrant(string privilege) =>
        new(privilege, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null);

    private static PrivilegeGrant ArmScopedGrant(string privilege, params Guid[] armIds) =>
        new(privilege, ScopeType.ArmList, armIds.ToHashSet(), SessionId: null);
}
