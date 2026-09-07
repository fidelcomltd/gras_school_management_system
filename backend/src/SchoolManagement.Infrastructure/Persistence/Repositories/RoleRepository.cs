using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Security.Roles;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IRoleRepository"/>.</summary>
internal sealed class RoleRepository(ApplicationDbContext context) : IRoleRepository
{
    /// <inheritdoc />
    public Task AddAsync(Role role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);
        cancellationToken.ThrowIfCancellationRequested();

        context.Roles.Add(role);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Role?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Roles.FirstOrDefaultAsync(role => role.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<Role?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Roles.AsNoTracking().FirstOrDefaultAsync(role => role.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task RemoveAsync(Role role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);
        cancellationToken.ThrowIfCancellationRequested();

        context.Roles.Remove(role);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> NameExistsAsync(string normalizedNameKey, Guid? excludingId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedNameKey);

        var query = context.Roles
            .AsNoTracking()
            .Where(role => role.NameKey == normalizedNameKey);

        if (excludingId is { } id)
        {
            query = query.Where(role => role.Id != id);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Raw SQL, same reason as <c>AdminAccountRepository.ListAsync</c>: C# has no relational
    /// <c>&gt;</c>/<c>&lt;</c> operator on <see cref="string"/> to express a keyset tie-break in LINQ.
    /// Unlike that method, the sort FIELD itself is caller-chosen between <c>name_key</c> and
    /// <c>status</c> (approved delta §2's whitelist) — both are already validated against a two-value
    /// whitelist by <see cref="ListRolesQueryValidator"/> before this is ever called, so choosing the
    /// column and the comparison operator via plain C# string selection (never through a
    /// parameter placeholder — a parameter cannot stand in for a column name or operator) is safe:
    /// nothing here is built from arbitrary caller text. Every actual VALUE (status text, search term,
    /// cursor value, page size) is still passed as a genuine bound parameter via
    /// <c>SqlQueryRaw</c>'s <c>{0}</c>-style placeholders.
    /// </remarks>
    public async Task<CursorPage<RoleDto>> ListAsync(
        RoleStatus? status,
        string? search,
        string sort,
        bool descending,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sort);

        var hasCursor = RoleListCursor.TryDecode(cursor, out var cursorSortValue, out var cursorId);

        var sortByStatus = string.Equals(sort, "status", StringComparison.Ordinal);
        var sortColumn = sortByStatus ? "status" : "name_key";
        var comparisonOperator = descending ? "<" : ">";
        var orderDirection = descending ? "DESC" : "ASC";

        var statusFilterGiven = status is not null;
        var statusFilterValue = status?.ToString() ?? string.Empty;
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        // {0} statusFilterGiven, {1} statusFilterValue, {2} searchTerm, {3} hasCursor,
        // {4} cursorSortValue, {5} cursorId, {6} pageSize+1 — every hole is a genuine bound VALUE;
        // sortColumn/comparisonOperator/orderDirection are literal SQL text, spliced in via ordinary
        // C# interpolation (the $$ prefix below means a DOUBLE brace is this literal's own
        // interpolation hole, so the single-brace {0}..{6} placeholders are untouched literal text
        // that SqlQueryRaw itself parses).
        var sql = $$"""
            SELECT id, name, description, is_system, privileges, status, name_key
            FROM roles
            WHERE
                (
                    ({0} = FALSE AND status = 'Active')
                    OR ({0} = TRUE AND status = {1}::varchar(20))
                )
                AND (
                    {2}::text IS NULL
                    OR name ILIKE '%' || {2}::text || '%'
                )
                AND (
                    {3} = FALSE
                    OR {{sortColumn}} {{comparisonOperator}} {4}::text
                    OR ({{sortColumn}} = {4}::text AND id {{comparisonOperator}} {5}::uuid)
                )
            ORDER BY {{sortColumn}} {{orderDirection}}, id {{orderDirection}}
            LIMIT {6}
            """;

        var rows = await context.Database
            .SqlQueryRaw<RoleListRow>(
                sql,
                statusFilterGiven,
                statusFilterValue,
                (object?)searchTerm ?? DBNull.Value,
                hasCursor,
                cursorSortValue,
                cursorId,
                pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasNextPage = rows.Count > pageSize;
        var page = hasNextPage ? rows.Take(pageSize).ToList() : rows;

        var items = page
            .Select(row => new RoleDto(
                row.Id.ToString("D", CultureInfo.InvariantCulture),
                row.Name,
                row.Description,
                row.IsSystem,
                string.IsNullOrEmpty(row.Privileges)
                    ? []
                    : row.Privileges.Split(',', StringSplitOptions.RemoveEmptyEntries),
                Enum.Parse<RoleStatus>(row.Status)))
            .ToArray();

        string? nextCursor = null;

        if (hasNextPage)
        {
            var last = page[^1];
            var lastSortValue = sortByStatus ? last.Status : last.NameKey;
            nextCursor = RoleListCursor.Encode(lastSortValue, last.Id);
        }

        return new CursorPage<RoleDto>(items, nextCursor);
    }

    /// <summary>Materialisation shape for the raw <see cref="ListAsync"/> query — never exposed over HTTP.</summary>
    private sealed class RoleListRow
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string? Description { get; init; }

        public bool IsSystem { get; init; }

        public string Privileges { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public string NameKey { get; init; } = string.Empty;
    }
}
