using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Roles;

/// <summary>
/// The wire shape of a role (spec 6.1.4; approved delta <c>.agent/decisions/2026-Q3-contract-deltas.md</c>
/// entry <c>TASK-0028</c> §2). Returned by every role endpoint — create, get, list (as the item
/// shape) and update all share this one DTO, matching how <c>AdminAccountDetailDto</c> is reused
/// across its own family.
/// </summary>
/// <param name="Id">Opaque identifier.</param>
/// <param name="Name">1..60 characters.</param>
/// <param name="Description">0..300 characters, or <see langword="null"/>.</param>
/// <param name="IsSystem">
/// True only for the seeded Super Admin role. A system role cannot be edited, renamed, deleted or
/// have privileges removed (spec 6.1.4).
/// </param>
/// <param name="Privileges">Canonical codes, at least one, sorted deterministically.</param>
/// <param name="Status">
/// <see cref="RoleStatus.Archived"/> roles are excluded from the default list (spec 9.4) but remain
/// individually readable and editable (except by <c>PATCH</c>/<c>DELETE</c> only when
/// <paramref name="IsSystem"/> is true).
/// </param>
public sealed record RoleDto(
    string Id,
    string Name,
    string? Description,
    bool IsSystem,
    IReadOnlyList<string> Privileges,
    RoleStatus Status);
