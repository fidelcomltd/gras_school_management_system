using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Sessions;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IAcademicSessionRepository"/>.</summary>
internal sealed class AcademicSessionRepository(ApplicationDbContext context) : IAcademicSessionRepository
{
    /// <inheritdoc />
    public Task AddAsync(AcademicSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        context.AcademicSessions.Add(session);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AcademicSession?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.AcademicSessions.FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<AcademicSession?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.AcademicSessions.AsNoTracking().FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<AcademicSession?> FindActiveAsync(CancellationToken cancellationToken) =>
        context.AcademicSessions.FirstOrDefaultAsync(session => session.State == SessionState.Active, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var query = context.AcademicSessions.AsNoTracking().Where(session => session.Name == name);

        if (excludingId is { } id)
        {
            query = query.Where(session => session.Id != id);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AcademicSession?> FindOverlappingAsync(
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var query = context.AcademicSessions
            .AsNoTracking()
            .Where(session => session.StartDate <= endDate && session.EndDate >= startDate);

        if (excludingId is { } id)
        {
            query = query.Where(session => session.Id != id);
        }

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Raw SQL for the keyset filter alone, same reason as <c>RoleRepository.ListAsync</c>: C# has no
    /// relational <c>&lt;</c> operator on <see cref="string"/> to express "before this name" in LINQ.
    /// Unlike that method there is no caller-chosen sort or search term — the order is always
    /// <c>name DESC</c> (spec 6.3.8) — so every hole below is a genuine bound VALUE, and no column or
    /// operator name is ever spliced in.
    /// </remarks>
    public async Task<CursorPage<SessionDto>> ListAsync(
        SessionState? state,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var hasCursor = SessionListCursor.TryDecode(cursor, out var cursorName);
        var stateFilterGiven = state is not null;
        var stateFilterValue = state?.ToString() ?? string.Empty;

        // TASK-0039: arm_count is a per-row scalar subquery, not a JOIN + GROUP BY — a session with
        // zero arms must still return exactly one row (an inner join would drop it, and a LEFT JOIN
        // would need the same GROUP BY anyway), and this table is admin-configuration-sized.
        var sql = """
            SELECT
                id, name, start_date, end_date, state,
                (SELECT COUNT(*)::int FROM arms WHERE arms.session_id = academic_sessions.id) AS arm_count
            FROM academic_sessions
            WHERE
                ({0} = FALSE OR state = {1}::varchar(20))
                AND ({2} = FALSE OR name < {3}::varchar(9))
            ORDER BY name DESC
            LIMIT {4}
            """;

        var rows = await context.Database
            .SqlQueryRaw<SessionListRow>(sql, stateFilterGiven, stateFilterValue, hasCursor, cursorName, pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasNextPage = rows.Count > pageSize;
        var page = hasNextPage ? rows.Take(pageSize).ToList() : rows;

        var items = page
            .Select(row => new SessionDto(
                row.Id.ToString("D", CultureInfo.InvariantCulture),
                row.Name,
                row.StartDate,
                row.EndDate,
                Enum.Parse<SessionState>(row.State),
                row.ArmCount))
            .ToArray();

        var nextCursor = hasNextPage ? SessionListCursor.Encode(page[^1].Name) : null;

        return new CursorPage<SessionDto>(items, nextCursor);
    }

    /// <summary>Materialisation shape for the raw <see cref="ListAsync"/> query — never exposed over HTTP.</summary>
    private sealed class SessionListRow
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public DateOnly StartDate { get; init; }

        public DateOnly EndDate { get; init; }

        public string State { get; init; } = string.Empty;

        public int ArmCount { get; init; }
    }
}
