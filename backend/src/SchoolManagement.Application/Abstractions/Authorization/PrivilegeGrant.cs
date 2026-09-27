using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Abstractions.Authorization;

/// <summary>
/// One active role assignment's contribution to a caller's effective privilege set, "tagged with
/// the scope it arrived through" (spec 4.2).
/// </summary>
/// <param name="Privilege">The canonical privilege code (never an alias — resolve before storing).</param>
/// <param name="Scope">Whether this grant applies school-wide or to a named list of arms.</param>
/// <param name="ArmIds">
/// The arms this grant covers. Empty when <paramref name="Scope"/> is
/// <c>ScopeType.SchoolWide</c>.
/// </param>
/// <param name="SessionId">
/// The session the underlying assignment names, or <see langword="null"/> for the sessionless,
/// permanent Super Admin assignment (spec 4.2.2). Enforced through <see cref="AppliesToSession"/>.
/// </param>
public sealed record PrivilegeGrant(
    string Privilege,
    ScopeType Scope,
    IReadOnlySet<Guid> ArmIds,
    Guid? SessionId)
{
    /// <summary>
    /// Spec 4.2.1: a grant counts only "in the session the target belongs to" (TASK-0060). A sessionless grant (Super
    /// Admin) applies in every session. A <see langword="null"/> <paramref name="targetSessionId"/> means the target
    /// belongs to no session of its own (a setting, a level, a pupil with no open enrolment, a register-wide list), so
    /// nothing is filtered.
    /// </summary>
    public bool AppliesToSession(Guid? targetSessionId) =>
        SessionId is null || targetSessionId is null || SessionId == targetSessionId;
}
