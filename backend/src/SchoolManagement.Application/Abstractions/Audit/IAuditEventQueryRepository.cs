using SchoolManagement.Application.Audit;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Audit;

namespace SchoolManagement.Application.Abstractions.Audit;

/// <summary>
/// The audit trail's READ surface (spec 6.1.12, TASK-0049). Deliberately separate from
/// <see cref="IAuditEventRepository"/>, which TASK-0048 kept ADD-ONLY on purpose — see that
/// interface's own remarks reserving reads for this card. Filters are combinable; every method
/// sorts newest first (<c>occurred_at</c> then <c>id</c>, both descending — see
/// <see cref="AuditEventListCursor"/> for why <c>id</c> breaks the tie).
/// </summary>
public interface IAuditEventQueryRepository
{
    /// <summary>Cursor-paginated per spec 9.5 — never offset.</summary>
    Task<CursorPage<AuditEventDto>> ListAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        Guid? actorAdminId,
        string? action,
        string? entityType,
        string? entityId,
        AuditOutcome? outcome,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// The full filtered set, unpaginated and lazily evaluated — the caller must enumerate it
    /// (rather than materialise a list) so a large export never buffers the whole result in memory.
    /// </summary>
    IAsyncEnumerable<AuditEventDto> StreamAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        Guid? actorAdminId,
        string? action,
        string? entityType,
        string? entityId,
        AuditOutcome? outcome,
        CancellationToken cancellationToken);
}
