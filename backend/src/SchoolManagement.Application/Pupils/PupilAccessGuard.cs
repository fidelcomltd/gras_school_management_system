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
/// OPEN ENROLMENT (<c>IPupilArmOfRecordLookup</c>, real since TASK-0059) via <c>ScopeResolver</c> and
/// <c>PrivilegeDecision</c>, which fails EVERY caller closed — including a school-wide holder — the
/// moment a target pupil resolves to <c>ScopeResolution.Unresolvable</c> (any <c>Pending</c> pupil, or
/// one between enrolments). That would make <c>PATCH /pupils/{id}</c> unusable for the exact
/// biographical-editing purpose the route exists to serve, since TASK-0050 pupils are created
/// <c>Pending</c> and admission approval (TASK-0051) still does not exist. The data-dependent pattern
/// <c>AdminAccountEndpoints</c> already established (<c>UpdateAdminAccountCommandHandler</c>'s own
/// remarks) is the right shape here too: the route maps with <c>RequireAuthenticatedCaller()</c> and
/// the branching in each handler, together with <see cref="ResolveArmIds"/> below, IS the
/// enforcement.
/// <para>
/// <see cref="PupilAccessScope.ArmRestricted"/> now means what it always should have: the caller sees
/// exactly the pupils whose OPEN ENROLMENT names one of their granted arms (TASK-0059) — not "sees
/// nothing", which was the honest but incomplete answer while no enrolment existed. A pupil with no
/// open enrolment (still <c>Pending</c>, or between enrolments) is unresolvable and an arm-restricted
/// caller is refused it, the same fail-closed direction <c>PrivilegeDecision.IsAuthorized</c> takes
/// for <c>ScopeResolution.Unresolvable</c> generally.
/// Full background on why this bypasses the declarative mechanism at all: <c>backend/docs/ASSUMPTIONS.md</c> §2.27.
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

    /// <summary>
    /// Every arm id granted to <paramref name="grants"/> for <paramref name="privilege"/> across all
    /// of the caller's ARM-LIST-scoped grants (there may be more than one <c>role_assignment</c> row).
    /// Callers only need this once <see cref="Resolve"/> has already returned
    /// <see cref="PupilAccessScope.ArmRestricted"/> — a <see cref="PupilAccessScope.SchoolWide"/> or
    /// <see cref="PupilAccessScope.Forbidden"/> caller never consults an arm list at all.
    /// </summary>
    public static IReadOnlySet<Guid> ResolveArmIds(IReadOnlyCollection<PrivilegeGrant> grants, string privilege)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentException.ThrowIfNullOrWhiteSpace(privilege);

        var canonical = PrivilegeAliases.Resolve(privilege);

        return grants
            .Where(grant => string.Equals(grant.Privilege, canonical, StringComparison.Ordinal) && grant.Scope == ScopeType.ArmList)
            .SelectMany(grant => grant.ArmIds)
            .ToHashSet();
    }
}
