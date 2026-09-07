using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Security.Roles;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Abstractions.Security;

/// <summary>Persistence port for <see cref="Role"/>.</summary>
public interface IRoleRepository
{
    /// <summary>Adds a new role. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(Role role, CancellationToken cancellationToken);

    /// <summary>Loads a TRACKED role by id, for a command that will mutate it.</summary>
    Task<Role?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a read-only projection-friendly role by id, for a query. <c>AsNoTracking</c>.</summary>
    Task<Role?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Removes <paramref name="role"/> permanently (spec 9.4: hard delete is permitted only "when no
    /// assignment has ever used it" — TASK-0028 has no <c>role_assignment</c> table at all, so this
    /// is unconditional here; TASK-0030 must add the has-ever-been-assigned branch, per the approved
    /// delta §4 and the STATE.md live-drift entry). No <c>SaveChangesAsync</c>.
    /// </summary>
    Task RemoveAsync(Role role, CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="normalizedNameKey"/> (already lower-invariant) is already used by
    /// another role — spec 6.1.4: "Unique, case-insensitive." <paramref name="excludingId"/> excludes
    /// the role being edited from its own uniqueness check.
    /// </summary>
    Task<bool> NameExistsAsync(string normalizedNameKey, Guid? excludingId, CancellationToken cancellationToken);

    /// <summary>
    /// Cursor-paginated, filtered, sorted list projection (spec 9.4, 9.5; approved delta §2) —
    /// <c>AsNoTracking</c>, projected straight to <see cref="RoleDto"/>. Archived roles are excluded
    /// unless <paramref name="status"/> names them explicitly (spec 9.4's default-scope rule,
    /// applied here at the data-access layer rather than per caller).
    /// </summary>
    /// <param name="status"><see langword="null"/> to use the default (active only).</param>
    /// <param name="search">Case-insensitive substring match against name, or <see langword="null"/>.</param>
    /// <param name="sort">Either <c>"name"</c> or <c>"status"</c> — validated by the caller.</param>
    /// <param name="descending">Whether to sort in descending order.</param>
    /// <param name="cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="pageSize">Already clamped to <see cref="CursorPageRequest.MaxPageSize"/> by the caller.</param>
    /// <param name="cancellationToken">Propagated to the underlying query.</param>
    Task<CursorPage<RoleDto>> ListAsync(
        RoleStatus? status,
        string? search,
        string sort,
        bool descending,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken);
}
