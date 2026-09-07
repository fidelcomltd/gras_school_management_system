using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Auth.AdminAccounts;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Auth;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IAdminAccountRepository"/>.</summary>
internal sealed class AdminAccountRepository(
    ApplicationDbContext context,
    DbContextOptions<ApplicationDbContext> options)
    : IAdminAccountRepository
{
    /// <inheritdoc />
    public Task<bool> AnyExistsAsync(CancellationToken cancellationToken) =>
        context.AdminAccounts.AsNoTracking().AnyAsync(cancellationToken);

    /// <inheritdoc />
    public Task AddAsync(AdminAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        cancellationToken.ThrowIfCancellationRequested();

        context.AdminAccounts.Add(account);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AdminAccount?> FindTrackedByEmailAsync(string email, CancellationToken cancellationToken) =>
        context.AdminAccounts.FirstOrDefaultAsync(account => account.Email == email, cancellationToken);

    /// <inheritdoc />
    public Task<AdminAccount?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.AdminAccounts.FirstOrDefaultAsync(account => account.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<AdminAccount?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.AdminAccounts.AsNoTracking().FirstOrDefaultAsync(account => account.Id == id, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Deliberately opens its OWN <see cref="ApplicationDbContext"/> over the same
    /// <see cref="DbContextOptions{TContext}"/> rather than reusing the scoped <c>context</c>
    /// field — see the interface member's remarks for why this write must be isolated from whatever
    /// transaction the ambient unit of work has open on the scoped context.
    /// </remarks>
    public async Task PersistLockoutStateAsync(AdminAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        await using var isolatedContext = new ApplicationDbContext(options);

        await isolatedContext.AdminAccounts
            .Where(row => row.Id == account.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.FailedLoginCount, account.FailedLoginCount)
                    .SetProperty(row => row.LastFailedLoginAtUtc, account.LastFailedLoginAtUtc)
                    .SetProperty(row => row.LockedUntilUtc, account.LockedUntilUtc),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> EmailExistsActiveOrSuspendedAsync(
        string normalizedEmail,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);

        var query = context.AdminAccounts
            .AsNoTracking()
            .Where(account => account.Email == normalizedEmail)
            .Where(account => account.Status != AdminAccountStatus.Deactivated);

        if (excludingId is { } id)
        {
            query = query.Where(account => account.Id != id);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Raw SQL rather than LINQ: spec 6.1.8's default sort ("status ascending... then staff name
    /// ascending") is a COMPOSITE keyset — C# has no relational <c>&gt;</c>/<c>&lt;</c> operator on
    /// <see cref="string"/> to express the tie-break in LINQ, but PostgreSQL's row-value comparison
    /// (<c>(status, staff_name, id) &gt; (@x, @y, @z)</c>) expresses exactly this and is the standard
    /// idiom for keyset pagination on a composite key.
    /// </remarks>
    public async Task<CursorPage<AdminAccountSummaryDto>> ListAsync(
        AdminAccountStatus? status,
        string? search,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // TryDecode always assigns its out parameters (to zero-value defaults on a null/invalid
        // cursor), so calling it unconditionally — rather than short-circuiting on `cursor is not
        // null` — is what keeps the three variables below definitely assigned for the interpolation
        // further down, regardless of which branch this takes.
        var hasCursor = AdminAccountListCursor.TryDecode(
            cursor,
            out var cursorStatusOrder,
            out var cursorStaffNameKey,
            out var cursorId);

        var cursorStatusText = hasCursor ? ((AdminAccountStatus)cursorStatusOrder).ToString() : string.Empty;

        var statusFilterGiven = status is not null;
        var statusFilterValue = status?.ToString() ?? string.Empty;

        // Deactivated excluded unless the status filter names it explicitly (spec 6.1.8).
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        // NO column aliases: SqlQuery<T> resolves each expected column by running the SAME snake_case
        // naming convention (UseSnakeCaseNamingConvention, registered globally) over
        // AdminAccountListRow's property names, so "CreatedAtUtc" is looked up as "created_at_utc" —
        // the raw column name already matches, and an "AS "PascalCase"" alias would instead break the
        // lookup (Postgres preserves quoted-identifier case, so it stops matching the convention's
        // transformed name).
        var rows = await context.Database
            .SqlQuery<AdminAccountListRow>(
                $"""
                SELECT id, staff_name, email, phone, status, is_super_admin, must_change_password,
                       last_login_at_utc, created_at_utc
                FROM admin_accounts
                WHERE
                    (
                        ({statusFilterGiven} = FALSE AND status <> 'Deactivated')
                        OR ({statusFilterGiven} = TRUE AND status = {statusFilterValue}::varchar(20))
                    )
                    AND (
                        {searchTerm}::text IS NULL
                        OR staff_name ILIKE '%' || {searchTerm}::text || '%'
                        OR email ILIKE '%' || {searchTerm}::text || '%'
                    )
                    AND (
                        {hasCursor} = FALSE
                        OR (status, lower(staff_name), id) >
                           ({cursorStatusText}::varchar(20), {cursorStaffNameKey}::text, {cursorId}::uuid)
                    )
                ORDER BY status ASC, lower(staff_name) ASC, id ASC
                LIMIT {pageSize + 1}
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasNextPage = rows.Count > pageSize;
        var page = hasNextPage ? rows.Take(pageSize).ToList() : rows;

        var items = page
            .Select(row => new AdminAccountSummaryDto(
                row.Id.ToString("D", CultureInfo.InvariantCulture),
                row.StaffName,
                row.Email,
                row.Phone,
                Enum.Parse<AdminAccountStatus>(row.Status),
                row.IsSuperAdmin,
                row.MustChangePassword,
                row.LastLoginAtUtc,
                row.CreatedAtUtc))
            .ToArray();

        string? nextCursor = null;

        if (hasNextPage)
        {
            var last = page[^1];
            nextCursor = AdminAccountListCursor.Encode(
                (int)Enum.Parse<AdminAccountStatus>(last.Status),
                last.StaffName.ToLowerInvariant(),
                last.Id);
        }

        return new CursorPage<AdminAccountSummaryDto>(items, nextCursor);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> LockActiveSuperAdminIdsAsync(CancellationToken cancellationToken)
    {
        var ids = await context.Database
            .SqlQuery<Guid>(
                $"""
                SELECT id FROM admin_accounts
                WHERE is_super_admin = TRUE AND status = 'Active'
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ids;
    }

    /// <summary>Materialisation shape for the raw <see cref="ListAsync"/> query — never exposed over HTTP.</summary>
    private sealed class AdminAccountListRow
    {
        public Guid Id { get; init; }

        public string StaffName { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public string? Phone { get; init; }

        public string Status { get; init; } = string.Empty;

        public bool IsSuperAdmin { get; init; }

        public bool MustChangePassword { get; init; }

        public DateTimeOffset? LastLoginAtUtc { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; }
    }
}
