using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Pupils;

/// <summary>Handles <see cref="ListAdmissionsQueueQuery"/>.</summary>
internal sealed class ListAdmissionsQueueQueryHandler(IPupilRepository pupils, TimeProvider timeProvider)
    : IRequestHandler<ListAdmissionsQueueQuery, Result<CursorPage<PupilDto>>>
{
    /// <inheritdoc />
    public async Task<Result<CursorPage<PupilDto>>> HandleAsync(
        ListAdmissionsQueueQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Cursor is not null && !PupilListCursor.TryDecode(request.Cursor, out _, out _))
        {
            return Result.Failure<CursorPage<PupilDto>>(Error.Validation(
                "admissions.invalid_cursor", "The cursor is invalid or has expired. Start again from the first page."));
        }

        var pageSize = Math.Clamp(request.PageSize ?? CursorPageRequest.DefaultPageSize, 1, CursorPageRequest.MaxPageSize);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var page = await pupils.ListAdmissionsQueueAsync(request.Cursor, pageSize, today, cancellationToken).ConfigureAwait(false);

        return Result.Success(page);
    }
}
