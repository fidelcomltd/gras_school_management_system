namespace SchoolManagement.Application.Abstractions.Authorization;

/// <summary>
/// What a route's scope-bearing parameter names, so the server can resolve it to a target arm per
/// spec 4.2.1. Declared alongside the privilege on every scopable route (spec 9.2: "where the
/// privilege is scopable, the parameter that resolves the target arm").
/// </summary>
public enum ScopeParameterKind
{
    /// <summary>
    /// The privilege is not scopable, or the route has no per-request scope target at all. No
    /// route parameter is consulted; the check requires a school-wide grant.
    /// </summary>
    None = 0,

    /// <summary>The route parameter IS the arm id. "A request naming an arm resolves to that arm."</summary>
    Arm = 1,

    /// <summary>
    /// The route parameter is a pupil id. "A request naming a pupil resolves to the pupil's arm of
    /// record for the active term" — resolved from the open enrolment, never a client-supplied arm id.
    /// </summary>
    Pupil = 2,

    /// <summary>The route parameter is a result set id. "A request naming a result set resolves to that result set's arm."</summary>
    ResultSet = 3,

    /// <summary>
    /// The route parameter is a level id with no arm. "A request naming a level, with no arm,
    /// requires the privilege school-wide." The parameter's value is not consulted for scope
    /// matching — only a school-wide grant can ever satisfy it.
    /// </summary>
    Level = 4,
}
