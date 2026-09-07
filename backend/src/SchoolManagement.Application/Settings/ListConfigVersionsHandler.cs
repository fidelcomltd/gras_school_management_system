using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="ListConfigVersionsQuery"/>.</summary>
internal sealed class ListConfigVersionsQueryHandler(IConfigVersionRepository repository)
    : IRequestHandler<ListConfigVersionsQuery, Result<CursorPage<ConfigVersionSummaryDto>>>
{
    /// <inheritdoc />
    public async Task<Result<CursorPage<ConfigVersionSummaryDto>>> HandleAsync(
        ListConfigVersionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        long? before = null;

        if (request.Cursor is not null)
        {
            if (!OpaqueCursor.TryDecode(request.Cursor, out var decoded))
            {
                return Result.Failure<CursorPage<ConfigVersionSummaryDto>>(Error.Validation(
                    "config_versions.invalid_cursor",
                    "The cursor is invalid or has expired. Start again from the first page."));
            }

            before = decoded;
        }

        var pageSize = Math.Clamp(
            request.PageSize ?? CursorPageRequest.DefaultPageSize,
            1,
            CursorPageRequest.MaxPageSize);

        var page = await repository.ListAsync(before, pageSize, cancellationToken).ConfigureAwait(false);

        return Result.Success(page);
    }
}
