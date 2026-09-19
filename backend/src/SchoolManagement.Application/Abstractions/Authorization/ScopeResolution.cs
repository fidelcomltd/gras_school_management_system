namespace SchoolManagement.Application.Abstractions.Authorization;

/// <summary>
/// The outcome of resolving a route's scope parameter per spec 4.2.1.
/// </summary>
public abstract record ScopeResolution
{
    private ScopeResolution()
    {
    }

    /// <summary>The scope parameter resolved to a single arm.</summary>
    /// <param name="ArmId">The resolved arm.</param>
    public sealed record ResolvedArm(Guid ArmId) : ScopeResolution;

    /// <summary>
    /// The target has no single arm (a level named with no arm) — spec 4.2.1: "requires the
    /// privilege school-wide." Only a school-wide grant can satisfy this outcome.
    /// </summary>
    public sealed record RequiresSchoolWide : ScopeResolution;

    /// <summary>
    /// The scope parameter was missing, or the named pupil/result set could not be resolved to an
    /// arm (for example it does not exist, or a pupil has no open enrolment). Fails closed: this
    /// outcome is never authorized, regardless of what the caller holds.
    /// </summary>
    public sealed record Unresolvable : ScopeResolution;

    /// <summary>
    /// The privilege is not scopable (<see cref="ScopeParameterKind.None"/>) — there is no scope
    /// target to resolve, and the check falls back to requiring a school-wide grant.
    /// </summary>
    public sealed record NotApplicable : ScopeResolution;

    /// <summary>
    /// Any active grant for the privilege satisfies the check, school-wide or arm-scoped alike —
    /// unlike <see cref="NotApplicable"/>, an arm-scoped grant is NOT rejected here (TASK-0086
    /// stage B). For a route with no single resolvable target at all (a class list, not one class's
    /// record) where an arm-scoped holder should still pass — the remark-template routes are the
    /// first caller: a class teacher's grant for <c>result.remark.classteacher</c> is ordinarily
    /// arm-scoped, and there is no arm in the route to resolve it against.
    /// </summary>
    /// <remarks>
    /// Constructed directly by a HANDLER (<c>RemarkTemplateAccessGuard</c>) that calls
    /// <c>PrivilegeDecision.IsAuthorized</c> itself, the same "route maps with
    /// <c>RequireAuthenticatedCaller()</c>, the handler does the data-dependent check" shape
    /// <c>PupilAccessGuard</c> already established — NOT wired through
    /// <see cref="ScopeParameterKind"/>/<c>ScopeResolver</c>, because which of two DIFFERENT
    /// privileges (<c>result.remark.classteacher</c> vs <c>result.remark.headteacher</c>) applies
    /// depends on a request's <c>kind</c> (query string, body field, or a stored row), never on a
    /// route parameter the declarative mechanism can read. Like every other resolution here, this
    /// does not consult <c>PrivilegeGrant.SessionId</c> — the same pre-existing gap
    /// TODO(TASK-0060) on <c>PrivilegeDecision</c> describes, not newly introduced or widened by
    /// this case.
    /// </remarks>
    public sealed record AnyGrant : ScopeResolution;
}
