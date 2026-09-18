using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Audit;

/// <summary>Handles <see cref="ListAuditEventsQuery"/>.</summary>
internal sealed class ListAuditEventsQueryHandler(IAuditEventQueryRepository auditEvents)
    : IRequestHandler<ListAuditEventsQuery, Result<CursorPage<AuditEventDto>>>
{
    /// <inheritdoc />
    public async Task<Result<CursorPage<AuditEventDto>>> HandleAsync(
        ListAuditEventsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Cursor is not null &&
            !AuditEventListCursor.TryDecode(request.Cursor, out _, out _))
        {
            return Result.Failure<CursorPage<AuditEventDto>>(Error.Validation(
                "audit_events.invalid_cursor",
                "The cursor is invalid or has expired. Start again from the first page."));
        }

        var pageSize = Math.Clamp(
            request.PageSize ?? CursorPageRequest.DefaultPageSize,
            1,
            CursorPageRequest.MaxPageSize);

        var page = await auditEvents
            .ListAsync(
                request.FromUtc,
                request.ToUtc,
                request.ActorAdminId,
                request.Action,
                request.EntityType,
                request.EntityId,
                request.Outcome,
                request.Cursor,
                pageSize,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(page);
    }
}
