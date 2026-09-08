using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Classes;

/// <summary>Handles <see cref="ReorderLevelsCommand"/>.</summary>
internal sealed class ReorderLevelsHandler(
    IClassLevelRepository levels,
    ISectionRepository sections,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<ReorderLevelsCommand, Result<IReadOnlyList<LevelDto>>>
{
    private const string EntityType = "class_level";

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<LevelDto>>> HandleAsync(
        ReorderLevelsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var orderedIds = request.OrderedLevelIds.Select(Guid.Parse).ToArray();
        var allLevels = (await levels.ListAllTrackedAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var byId = allLevels.ToDictionary(level => level.Id);

        var activeIds = allLevels.Where(level => level.Status == LevelStatus.Active).Select(level => level.Id).ToHashSet();
        var providedIds = orderedIds.ToHashSet();

        if (!activeIds.SetEquals(providedIds))
        {
            return Result.Failure<IReadOnlyList<LevelDto>>(Error.Validation(
                "level.reorder_set_mismatch",
                "The reordered list must contain exactly every currently active level, each exactly once."));
        }

        // See IClassLevelRepository.NegateProgressionOrdersAsync's remarks: the partial unique index
        // on progression_order is checked per row, immediately, so writing every row straight to its
        // final value risks a mid-batch collision (proven empirically, not assumed) whenever the
        // requested order is anything other than a no-op. Negating first moves every row to a value
        // nothing else in the table can hold.
        await levels.NegateProgressionOrdersAsync(orderedIds, cancellationToken).ConfigureAwait(false);

        for (var index = 0; index < orderedIds.Length; index++)
        {
            var level = byId[orderedIds[index]];
            level.SetProgressionOrder(index + 1);

            var nextId = index + 1 < orderedIds.Length ? orderedIds[index + 1] : (Guid?)null;
            var setNext = level.SetNextLevel(nextId);

            if (setNext.IsFailure)
            {
                return Result.Failure<IReadOnlyList<LevelDto>>(setNext.Error);
            }
        }

        var namesById = allLevels.ToDictionary(level => level.Id, level => level.Name);
        var chainCheck = ProgressionChainGuard.Validate(LevelMapper.ToChainLevels(allLevels), namesById);

        if (chainCheck.IsFailure)
        {
            return Result.Failure<IReadOnlyList<LevelDto>>(chainCheck.Error);
        }

        await auditSink.RecordAsync(
            Privileges.Level.Update,
            EntityType,
            entityId: null,
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["count"] = orderedIds.Length },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        var allSections = await sections.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var sectionNamesById = allSections.ToDictionary(section => section.Id, section => section.Name);

        return Result.Success<IReadOnlyList<LevelDto>>(
            orderedIds.Select(id => LevelMapper.ToDto(byId[id], allLevels, sectionNamesById)).ToArray());
    }
}
