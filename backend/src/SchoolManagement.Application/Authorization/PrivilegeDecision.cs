using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Authorization;

/// <summary>
/// The single, pure decision at the heart of the authorisation substrate: given what a caller
/// holds and what a route resolved, is the request allowed? Spec 4.2.1: "does the account hold the
/// required privilege through at least one active assignment whose scope is school-wide, or whose
/// arm list contains the resolved arm."
/// </summary>
/// <remarks>
/// Deliberately a pure function over plain data — no <c>HttpContext</c>, no DI — so every branch is
/// a fast, exhaustive unit test rather than an HTTP round trip.
/// <para>
/// SESSION BOUNDARY (TASK-0060): a resolved arm carries its session, and only a grant in that session (or the
/// sessionless Super Admin grant) covers it, per <see cref="PrivilegeGrant.AppliesToSession"/>. Arm-list grants could
/// not leak anyway, because an assignment's arms must belong to its session; the school-wide grant is what this stops.
/// A pupil with no open enrolment resolves to <see cref="ScopeResolution.RequiresSchoolWide"/> in the active session.
/// A level, <see cref="ScopeResolution.NotApplicable"/> and <see cref="ScopeResolution.AnyGrant"/> name no session and
/// are not filtered: whether non-scopable operations follow the active session is a spec 4.2.2 question left open. Handler-level checks
/// (<c>PupilAccessGuard</c>) apply the same filter with the session of the pupil, arm or report they target.
/// </para>
/// </remarks>
public static class PrivilegeDecision
{
    /// <summary>Decides whether <paramref name="grants"/> satisfy <paramref name="privilege"/> for <paramref name="resolution"/>.</summary>
    /// <param name="grants">The caller's effective privilege set.</param>
    /// <param name="privilege">The privilege the route requires (an alias is resolved before matching).</param>
    /// <param name="resolution">What the route's scope parameter resolved to.</param>
    public static bool IsAuthorized(
        IReadOnlyCollection<PrivilegeGrant> grants,
        string privilege,
        ScopeResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(privilege);
        ArgumentNullException.ThrowIfNull(resolution);

        var canonicalPrivilege = PrivilegeAliases.Resolve(privilege);

        var matching = grants.Where(grant => string.Equals(grant.Privilege, canonicalPrivilege, StringComparison.Ordinal));

        return resolution switch
        {
            // Non-scopable privilege, or a level named with no arm: only a school-wide grant can
            // ever satisfy it. An arm-scoped grant — however wide its arm list — does not count,
            // by design (spec 4.2.1).
            ScopeResolution.NotApplicable => matching.Any(grant => grant.Scope == ScopeType.SchoolWide),
            ScopeResolution.RequiresSchoolWide requiresSchoolWide => matching.Any(grant =>
                grant.Scope == ScopeType.SchoolWide && grant.AppliesToSession(requiresSchoolWide.SessionId)),

            // A resolved arm: a grant in the arm's session covers it when school-wide, or when the arm is in
            // that specific grant's list.
            ScopeResolution.ResolvedArm resolvedArm => matching.Any(grant =>
                grant.AppliesToSession(resolvedArm.SessionId) &&
                (grant.Scope == ScopeType.SchoolWide ||
                (grant.Scope == ScopeType.ArmList && grant.ArmIds.Contains(resolvedArm.ArmId)))),

            // The target could not be resolved at all. Fails closed rather than falling back to "any
            // grant will do" — an unresolvable target must never be treated as in-scope.
            ScopeResolution.Unresolvable => false,

            // TASK-0086 stage B: any active grant for the privilege satisfies it, school-wide or
            // arm-scoped alike — see ScopeResolution.AnyGrant's remarks for why this exists
            // (there is no route-declared target to resolve an arm-scoped grant against).
            ScopeResolution.AnyGrant => matching.Any(),

            _ => false,
        };
    }
}
