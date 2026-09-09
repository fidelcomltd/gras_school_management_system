using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Audit;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Audit;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IAuditEventQueryRepository"/> (spec 6.1.12, TASK-0049).</summary>
/// <remarks>
/// Plain LINQ, not the raw-SQL row-value comparison <c>AdminAccountRepository.ListAsync</c> needs
/// for its string tie-break: the keyset here is <c>(occurred_at, id)</c>, both types EF Core
/// translates comparison operators for directly, so the OR-form keyset predicate below is enough —
/// see <see cref="AuditEventListCursor"/> for why the tie-break exists at all.
/// </remarks>
internal sealed class AuditEventQueryRepository(ApplicationDbContext context) : IAuditEventQueryRepository
{
    private static readonly Expression<Func<AuditEvent, AuditEventRow>> Projection = auditEvent => new AuditEventRow(
        auditEvent.Id,
        auditEvent.OccurredAt,
        auditEvent.ActorAdminId,
        auditEvent.ActorLabel,
        auditEvent.Action,
        auditEvent.EntityType,
        auditEvent.EntityId,
        auditEvent.Outcome,
        auditEvent.BeforeJson,
        auditEvent.AfterJson,
        auditEvent.Reason,
        auditEvent.SourceIp,
        auditEvent.UserAgent);

    /// <inheritdoc />
    public async Task<CursorPage<AuditEventDto>> ListAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        Guid? actorAdminId,
        string? action,
        string? entityType,
        AuditOutcome? outcome,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(context.AuditEvents.AsNoTracking(), fromUtc, toUtc, actorAdminId, action, entityType, outcome);

        if (AuditEventListCursor.TryDecode(cursor, out var cursorOccurredAt, out var cursorId))
        {
            // Composite keyset predicate for ORDER BY occurred_at DESC, id DESC: strictly older, or
            // exactly as old and strictly lower id — the same row can never reappear on a later page.
            query = query.Where(auditEvent =>
                auditEvent.OccurredAt < cursorOccurredAt ||
                (auditEvent.OccurredAt == cursorOccurredAt && auditEvent.Id < cursorId));
        }

        // Take one extra row to learn whether a further page exists, without a second COUNT query.
        var rows = await query
            .OrderByDescending(auditEvent => auditEvent.OccurredAt)
            .ThenByDescending(auditEvent => auditEvent.Id)
            .Take(pageSize + 1)
            .Select(Projection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasNextPage = rows.Count > pageSize;
        var page = hasNextPage ? rows.GetRange(0, pageSize) : rows;

        var items = page.ConvertAll(ToDto);

        var nextCursor = hasNextPage
            ? AuditEventListCursor.Encode(page[^1].OccurredAt, page[^1].Id)
            : null;

        return new CursorPage<AuditEventDto>(items, nextCursor);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<AuditEventDto> StreamAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        Guid? actorAdminId,
        string? action,
        string? entityType,
        AuditOutcome? outcome,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(context.AuditEvents.AsNoTracking(), fromUtc, toUtc, actorAdminId, action, entityType, outcome)
            .OrderByDescending(auditEvent => auditEvent.OccurredAt)
            .ThenByDescending(auditEvent => auditEvent.Id)
            .Select(Projection);

        return StreamRowsAsync(query, cancellationToken);
    }

    /// <summary>
    /// The lazy bridge from rows to DTOs: <see cref="ToDto"/> runs only as each row is pulled off the
    /// wire (id/Guid-to-string formatting is a client-side operation EF Core cannot translate into
    /// SQL), so the caller never holds more than one row's worth of the filtered set in memory at a
    /// time — the whole point of streaming a table this large (spec 6.1.12: seven-year retention,
    /// every score edit).
    /// </summary>
    private static async IAsyncEnumerable<AuditEventDto> StreamRowsAsync(
        IQueryable<AuditEventRow> query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return ToDto(row);
        }
    }

    private static IQueryable<AuditEvent> ApplyFilters(
        IQueryable<AuditEvent> query,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        Guid? actorAdminId,
        string? action,
        string? entityType,
        AuditOutcome? outcome)
    {
        if (fromUtc is { } from)
        {
            query = query.Where(auditEvent => auditEvent.OccurredAt >= from);
        }

        if (toUtc is { } to)
        {
            query = query.Where(auditEvent => auditEvent.OccurredAt <= to);
        }

        if (actorAdminId is { } actor)
        {
            query = query.Where(auditEvent => auditEvent.ActorAdminId == actor);
        }

        if (action is not null)
        {
            query = query.Where(auditEvent => auditEvent.Action == action);
        }

        if (entityType is not null)
        {
            query = query.Where(auditEvent => auditEvent.EntityType == entityType);
        }

        if (outcome is { } resolvedOutcome)
        {
            query = query.Where(auditEvent => auditEvent.Outcome == resolvedOutcome);
        }

        return query;
    }

    private static AuditEventDto ToDto(AuditEventRow row) => new(
        row.Id.ToString(CultureInfo.InvariantCulture),
        row.OccurredAt,
        row.ActorAdminId?.ToString("D", CultureInfo.InvariantCulture),
        row.ActorLabel,
        row.Action,
        row.EntityType,
        row.EntityId,
        row.Outcome,
        row.BeforeJson,
        row.AfterJson,
        row.Reason,
        row.SourceIp,
        row.UserAgent);

    /// <summary>Materialisation shape for the projected query — never exposed over HTTP.</summary>
    private sealed record AuditEventRow(
        long Id,
        DateTimeOffset OccurredAt,
        Guid? ActorAdminId,
        string ActorLabel,
        string Action,
        string EntityType,
        string? EntityId,
        AuditOutcome Outcome,
        string? BeforeJson,
        string? AfterJson,
        string? Reason,
        string? SourceIp,
        string? UserAgent);
}
