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
}
