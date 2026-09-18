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
/// SESSION BOUNDARY — STILL NOT ENFORCED HERE, and TASK-0059 does not change that, though the
/// original reason it does not (no session-bearing scope target had a real lookup) has partly gone:
/// <c>IPupilArmOfRecordLookup</c> is real since TASK-0059 (resolving a pupil's arm from their open
/// enrolment, spec 02 §5.2), which itself resolves to a session via the arm's own <c>SessionId</c>.
/// What has NOT changed is the routing: pupil routes still never declare
/// <see cref="SchoolManagement.Application.Abstractions.Authorization.ScopeParameterKind.Pupil"/>
/// through <c>ScopeResolver</c> — they use the handler-level, data-dependent
/// <c>SchoolManagement.Application.Pupils.PupilAccessGuard</c> pattern instead (see its own remarks
/// for why), so this method's <see cref="PrivilegeGrant.SessionId"/> dimension is still never
/// exercised by any real caller. Implementing the LOOKUP did not resolve the underlying TODO — a
/// route would have to use the declarative <c>ScopeParameterKind.Pupil</c>/<c>ResultSet</c>
/// mechanism for that, which is a routing decision outside this card's boundary (§07 arm-scoped
/// access is closed by <c>PupilAccessGuard</c>, not by this method). This is a genuinely separate,
/// cross-cutting concern spanning every scoped route, not only pupils, and is carded as
/// TASK-0060, opened at TASK-0059's closure.
/// This method therefore still ignores <see cref="PrivilegeGrant.SessionId"/> entirely.
/// TODO(TASK-0060): filter matching
/// grants by the target's session once a route actually resolves a target through
/// <c>ScopeParameterKind.Pupil</c>/<c>ScopeParameterKind.ResultSet</c>.
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
            ScopeResolution.RequiresSchoolWide => matching.Any(grant => grant.Scope == ScopeType.SchoolWide),

            // A resolved arm: a school-wide grant always covers it; an arm-list grant covers it only
            // if the resolved arm is in that specific grant's list.
            ScopeResolution.ResolvedArm resolvedArm => matching.Any(grant =>
                grant.Scope == ScopeType.SchoolWide ||
                (grant.Scope == ScopeType.ArmList && grant.ArmIds.Contains(resolvedArm.ArmId))),

            // The target could not be resolved at all. Fails closed rather than falling back to "any
            // grant will do" — an unresolvable target must never be treated as in-scope.
            ScopeResolution.Unresolvable => false,

            _ => false,
        };
    }
}
