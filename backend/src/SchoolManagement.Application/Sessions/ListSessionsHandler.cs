using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Sessions;

/// <summary>Handles <see cref="ListSessionsQuery"/>.</summary>
internal sealed class ListSessionsHandler(IAcademicSessionRepository sessions)
    : IRequestHandler<ListSessionsQuery, Result<CursorPage<SessionDto>>>
{
    /// <inheritdoc />
    public async Task<Result<CursorPage<SessionDto>>> HandleAsync(
        ListSessionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Cursor is not null && !SessionListCursor.TryDecode(request.Cursor, out _))
        {
            return Result.Failure<CursorPage<SessionDto>>(Error.Validation(
                "sessions.invalid_cursor",
                "The cursor is invalid or has expired. Start again from the first page."));
        }

        var pageSize = Math.Clamp(
            request.PageSize ?? CursorPageRequest.DefaultPageSize,
            1,
            CursorPageRequest.MaxPageSize);

        var page = await sessions
            .ListAsync(request.State, request.Cursor, pageSize, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(page);
    }
}
