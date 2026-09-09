using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Assignments;

/// <summary>Wire shape of a <see cref="RoleAssignment"/> (spec 6.1.5).</summary>
/// <param name="Id">Opaque identifier.</param>
/// <param name="AdminAccountId">The account this assignment grants a role to.</param>
/// <param name="RoleId">The role granted.</param>
/// <param name="SessionId">
/// The session this assignment applies to, or <see langword="null"/> only for the sessionless Super
/// Admin assignment — TASK-0030 never actually produces that case (see <see cref="RoleAssignment"/>'s
/// remarks) but the wire shape stays honest to the entity rather than asserting non-null.
/// </param>
/// <param name="ScopeType">Either <c>SchoolWide</c> or <c>ArmList</c>.</param>
/// <param name="ArmIds">Non-empty only when <paramref name="ScopeType"/> is <c>ArmList</c>.</param>
/// <param name="GrantedBy">The acting account that created this assignment.</param>
/// <param name="Status">Either <c>Active</c> or <c>Revoked</c>.</param>
/// <param name="CreatedAtUtc">When this assignment was created.</param>
public sealed record RoleAssignmentDto(
    string Id,
    string AdminAccountId,
    string RoleId,
    string? SessionId,
    ScopeType ScopeType,
    IReadOnlyList<string> ArmIds,
    string GrantedBy,
    RoleAssignmentStatus Status,
    DateTimeOffset CreatedAtUtc);
