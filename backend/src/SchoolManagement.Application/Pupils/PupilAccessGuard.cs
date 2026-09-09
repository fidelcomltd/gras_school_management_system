using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// What a caller's grants resolve to for a <c>pupil.*</c> scopable privilege — <c>ListPupils</c>,
/// <c>GetPupil</c> and <c>UpdatePupilBiographical</c> all branch on this.
/// </summary>
internal enum PupilAccessScope
{
    /// <summary>The caller holds no active grant for the privilege at all, in any scope.</summary>
    Forbidden,

    /// <summary>At least one active grant is school-wide. Unrestricted.</summary>
    SchoolWide,

    /// <summary>Every active grant is arm-scoped (a named list of arms), none school-wide.</summary>
    ArmRestricted,
}

/// <summary>
/// Resolves <see cref="PupilAccessScope"/> from a caller's real, DI-resolved grants
/// (<see cref="IEffectivePrivilegeProvider"/>) — the mechanism TASK-0030 shipped, per this card's own
/// instruction not to re-derive one.
/// </summary>
/// <remarks>
/// WHY THIS RUNS IN THE HANDLER, NOT AS A ROUTE-DECLARATIVE <c>RequirePrivilege(...,
/// ScopeParameterKind.Pupil, ...)</c> CHECK: that mechanism resolves a target pupil's arm from its
/// OPEN ENROLMENT (<c>IPupilArmOfRecordLookup</c>), which this card deliberately does not build — see
/// <c>Pupil</c>'s own remarks. Every pupil this card can create is <c>Pending</c> and so has NO arm at
/// all; feeding that through <c>ScopeParameterKind.Pupil</c> would resolve to
/// <c>ScopeResolution.Unresolvable</c>, which <c>PrivilegeDecision</c> fails closed for EVERY caller —
/// including a school-wide holder — which would make <c>PATCH /pupils/{id}</c> unusable for the exact
/// biographical-editing purpose this card exists to serve. The data-dependent pattern
/// <c>AdminAccountEndpoints</c> already established (<c>UpdateAdminAccountCommandHandler</c>'s own
/// remarks) is the right shape here too: the route maps with <c>RequireAuthenticatedCaller()</c> and
/// the branching below IS the enforcement.
/// <para>
/// <see cref="PupilAccessScope.ArmRestricted"/> therefore means "sees nothing" TODAY, honestly:
/// <c>Pupil</c> carries no arm reference at all (no enrolment exists), so an arm-scoped grant can
/// never match any row — not a bug, the correct answer given the current schema. Full rationale:
/// <c>backend/docs/ASSUMPTIONS.md</c> §2.27.
/// </para>
/// </remarks>
internal static class PupilAccessGuard
{
    /// <summary>Resolves <paramref name="grants"/> against <paramref name="privilege"/> (a <c>Privileges.Pupil.*</c> constant).</summary>
    public static PupilAccessScope Resolve(IReadOnlyCollection<PrivilegeGrant> grants, string privilege)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentException.ThrowIfNullOrWhiteSpace(privilege);

        var canonical = PrivilegeAliases.Resolve(privilege);
        var matching = grants.Where(grant => string.Equals(grant.Privilege, canonical, StringComparison.Ordinal)).ToArray();

        if (matching.Length == 0)
        {
            return PupilAccessScope.Forbidden;
        }

        return matching.Any(grant => grant.Scope == ScopeType.SchoolWide)
            ? PupilAccessScope.SchoolWide
            : PupilAccessScope.ArmRestricted;
    }
}
