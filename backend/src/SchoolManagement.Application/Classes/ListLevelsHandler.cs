using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>Handles <see cref="ListLevelsQuery"/>.</summary>
internal sealed class ListLevelsHandler(IClassLevelRepository levels, ISectionRepository sections)
    : IRequestHandler<ListLevelsQuery, Result<CursorPage<LevelDto>>>
{
    /// <inheritdoc />
    public async Task<Result<CursorPage<LevelDto>>> HandleAsync(
        ListLevelsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Cursor is not null && !LevelListCursor.TryDecode(request.Cursor, out _, out _))
        {
            return Result.Failure<CursorPage<LevelDto>>(Error.Validation(
                "level.invalid_cursor", "The cursor is invalid or has expired. Start again from the first page."));
        }

        var pageSize = Math.Clamp(request.PageSize ?? CursorPageRequest.DefaultPageSize, 1, CursorPageRequest.MaxPageSize);

        var allLevels = await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var hasCursor = LevelListCursor.TryDecode(request.Cursor, out var cursorOrder, out var cursorId);

        var candidates = allLevels
            .Where(level => request.IncludeInactive || level.Status == LevelStatus.Active)
            .OrderBy(level => level.ProgressionOrder)
            .ThenBy(level => level.Id)
            .Where(level => !hasCursor ||
                level.ProgressionOrder > cursorOrder ||
                (level.ProgressionOrder == cursorOrder && level.Id.CompareTo(cursorId) > 0))
            .ToArray();

        var page = candidates.Take(pageSize).ToArray();
        var hasNextPage = candidates.Length > pageSize;

        var allSections = await sections.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var sectionNamesById = allSections.ToDictionary(section => section.Id, section => section.Name);

        var items = page.Select(level => LevelMapper.ToDto(level, allLevels, sectionNamesById)).ToArray();

        var nextCursor = hasNextPage
            ? LevelListCursor.Encode(page[^1].ProgressionOrder, page[^1].Id)
            : null;

        return Result.Success(new CursorPage<LevelDto>(items, nextCursor));
    }
}
