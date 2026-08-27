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

    [Fact]
    public void NoGrantsAtAll_IsNeverAuthorized()
    {
        var authorized = PrivilegeDecision.IsAuthorized(
            [], Privileges.Results.View, new ScopeResolution.ResolvedArm(ArmA));

        authorized.ShouldBeFalse();
    }

    [Fact]
    public void AGrantForADifferentPrivilege_DoesNotAuthorize()
    {
        var grants = new[] { SchoolWideGrant(Privileges.Pupil.View) };

        var authorized = PrivilegeDecision.IsAuthorized(
            grants, Privileges.Results.View, new ScopeResolution.ResolvedArm(ArmA));

        authorized.ShouldBeFalse();
    }

    [Fact]
    public void SchoolWideGrant_AuthorizesAnyResolvedArm()
    {
        var grants = new[] { SchoolWideGrant(Privileges.Results.View) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.View, new ScopeResolution.ResolvedArm(ArmA))
            .ShouldBeTrue();
        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.View, new ScopeResolution.ResolvedArm(ArmB))
            .ShouldBeTrue();
    }

    [Fact]
    public void SchoolWideGrant_AuthorizesRequiresSchoolWideAndNotApplicable()
    {
        var grants = new[] { SchoolWideGrant(Privileges.Settings.GradingUpdate) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Settings.GradingUpdate, new ScopeResolution.RequiresSchoolWide())
            .ShouldBeTrue();
        PrivilegeDecision.IsAuthorized(grants, Privileges.Settings.GradingUpdate, new ScopeResolution.NotApplicable())
            .ShouldBeTrue();
    }

    [Fact]
    public void ArmScopedGrant_AuthorizesOnlyTheArmItLists()
    {
        var grants = new[] { ArmScopedGrant(Privileges.Results.ScoreEnter, ArmA) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.ScoreEnter, new ScopeResolution.ResolvedArm(ArmA))
            .ShouldBeTrue("the grant lists this arm");

        PrivilegeDecision.IsAuthorized(grants, Privileges.Results.ScoreEnter, new ScopeResolution.ResolvedArm(ArmB))
            .ShouldBeFalse("the grant does not list this arm — this is the out-of-scope case");
    }

    [Fact]
    public void ArmScopedGrant_NeverSatisfiesARequiresSchoolWideOutcome()
    {
        // The rule from spec 4.2.1 stated explicitly: "An arm-scoped holder cannot perform
        // level-wide operations even over a level containing only their own arm."
        var grants = new[] { ArmScopedGrant(Privileges.Promotion.Run, ArmA) };

        PrivilegeDecision.IsAuthorized(grants, Privileges.Promotion.Run, new ScopeResolution.RequiresSchoolWide())
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

    private static PrivilegeGrant SchoolWideGrant(string privilege) =>
        new(privilege, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null);

    private static PrivilegeGrant ArmScopedGrant(string privilege, params Guid[] armIds) =>
        new(privilege, ScopeType.ArmList, armIds.ToHashSet(), SessionId: null);
}
