using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Abstractions.Security;

/// <summary>Persistence port for <see cref="RoleAssignment"/>.</summary>
public interface IRoleAssignmentRepository
{
    /// <summary>Adds a new assignment. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(RoleAssignment assignment, CancellationToken cancellationToken);

    /// <summary>Loads a TRACKED assignment by id, for a command that will mutate it (revoke).</summary>
    Task<RoleAssignment?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Every assignment (active and revoked) for <paramref name="adminAccountId"/>, <c>AsNoTracking</c> —
    /// <c>GET /admins/{id}/assignments</c>.
    /// </summary>
    Task<IReadOnlyList<RoleAssignment>> ListForAccountReadOnlyAsync(
        Guid adminAccountId, CancellationToken cancellationToken);

    /// <summary>
    /// Every ACTIVE assignment for <paramref name="adminAccountId"/>, <c>AsNoTracking</c> — the
    /// graduated <c>IEffectivePrivilegeProvider</c>'s resolution source.
    /// </summary>
    Task<IReadOnlyList<RoleAssignment>> ListActiveForAccountReadOnlyAsync(
        Guid adminAccountId, CancellationToken cancellationToken);

    /// <summary>
    /// Every ACTIVE assignment for <paramref name="adminAccountId"/>, TRACKED — used to revoke every
    /// one of them in the same transaction that deactivates the account (spec 6.1.10).
    /// </summary>
    Task<IReadOnlyList<RoleAssignment>> ListActiveForAccountTrackedAsync(
        Guid adminAccountId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether any assignment (active or revoked) has ever referenced <paramref name="roleId"/> —
    /// spec 9.4's has-ever-been-assigned branch for <c>DELETE /roles/{id}</c>.
    /// </summary>
    Task<bool> ExistsForRoleAsync(Guid roleId, CancellationToken cancellationToken);
}
