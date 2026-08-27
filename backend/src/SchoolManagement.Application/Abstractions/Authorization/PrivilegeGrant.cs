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
/// permanent Super Admin assignment (spec 4.2.2). Not yet enforced against a target's session by
/// <c>PrivilegeDecision</c> — see the remarks there.
/// </param>
public sealed record PrivilegeGrant(
    string Privilege,
    ScopeType Scope,
    IReadOnlySet<Guid> ArmIds,
    Guid? SessionId);
