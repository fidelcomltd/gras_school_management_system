using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>Handles <see cref="ListArmsQuery"/>.</summary>
internal sealed class ListArmsHandler(IArmRepository arms, IClassLevelRepository levels)
    : IRequestHandler<ListArmsQuery, Result<CursorPage<ArmDto>>>
{
    /// <inheritdoc />
    public async Task<Result<CursorPage<ArmDto>>> HandleAsync(ListArmsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Cursor is not null && !ArmListCursor.TryDecode(request.Cursor, out _, out _, out _))
        {
            return Result.Failure<CursorPage<ArmDto>>(Error.Validation(
                "arm.invalid_cursor", "The cursor is invalid or has expired. Start again from the first page."));
        }

        var pageSize = Math.Clamp(request.PageSize ?? CursorPageRequest.DefaultPageSize, 1, CursorPageRequest.MaxPageSize);

        var allLevels = await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var levelsById = allLevels.ToDictionary(level => level.Id);
        var levelNamesById = allLevels.ToDictionary(level => level.Id, level => level.Name);

        var sessionFilter = request.SessionId is null ? (Guid?)null : Guid.Parse(request.SessionId);
        var levelFilter = request.LevelId is null ? (Guid?)null : Guid.Parse(request.LevelId);
        var formTeacherFilter = request.FormTeacherAdminId is null ? (Guid?)null : Guid.Parse(request.FormTeacherAdminId);

        var allArms = await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);

        var filtered = allArms
            .Where(arm => sessionFilter is null || arm.SessionId == sessionFilter)
            .Where(arm => levelFilter is null || arm.ClassLevelId == levelFilter)
            .Where(arm => request.Status is null || arm.Status == request.Status)
            .Where(arm => formTeacherFilter is null || arm.FormTeacherAdminId == formTeacherFilter)
            .Where(arm => levelsById.ContainsKey(arm.ClassLevelId))
            .Where(arm => request.Label is null ||
                ArmDisplayName.Compose(levelNamesById[arm.ClassLevelId], arm.Label)
                    .Contains(request.Label, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var hasCursor = ArmListCursor.TryDecode(request.Cursor, out var cursorOrder, out var cursorLabel, out var cursorId);

        var ordered = filtered
            .OrderBy(arm => levelsById[arm.ClassLevelId].ProgressionOrder)
            .ThenBy(arm => arm.Label, ArmLabelComparer.Instance)
            .ThenBy(arm => arm.Id)
            .ToArray();

        var candidates = ordered
            .Where(arm => !hasCursor || IsAfterCursor(arm, levelsById[arm.ClassLevelId].ProgressionOrder, cursorOrder, cursorLabel, cursorId))
            .ToArray();

        var page = candidates.Take(pageSize).ToArray();
        var hasNextPage = candidates.Length > pageSize;

        var items = page.Select(arm => ArmMapper.ToDto(arm, levelNamesById)).ToArray();

        var nextCursor = hasNextPage
            ? ArmListCursor.Encode(levelsById[page[^1].ClassLevelId].ProgressionOrder, page[^1].Label, page[^1].Id)
            : null;

        return Result.Success(new CursorPage<ArmDto>(items, nextCursor));
    }

    private static bool IsAfterCursor(Arm arm, int progressionOrder, int cursorOrder, string cursorLabel, Guid cursorId)
    {
        if (progressionOrder != cursorOrder)
        {
            return progressionOrder > cursorOrder;
        }

        var labelComparison = ArmLabelComparer.Instance.Compare(arm.Label, cursorLabel);

        return labelComparison != 0 ? labelComparison > 0 : arm.Id.CompareTo(cursorId) > 0;
    }
}
