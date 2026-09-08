using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Classes;

/// <summary>Handles <see cref="CreateLevelCommand"/>.</summary>
internal sealed class CreateLevelHandler(
    IClassLevelRepository levels,
    ISectionRepository sections,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<CreateLevelCommand, Result<LevelDto>>
{
    private const string EntityType = "class_level";

    /// <inheritdoc />
    public async Task<Result<LevelDto>> HandleAsync(CreateLevelCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allSections = await sections.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var sectionId = Guid.Parse(request.SectionId);

        if (allSections.All(section => section.Id != sectionId))
        {
            return Result.Failure<LevelDto>(Error.Validation(
                "level.section_not_found", "No section was found with that id."));
        }

        var trimmedName = request.Name.Trim();
        var nameKey = trimmedName.ToLowerInvariant();

        if (await levels.NameExistsAsync(nameKey, excludingId: null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<LevelDto>(Error.Conflict(
                "level.name_duplicate", $"A class level named {trimmedName} already exists."));
        }

        var allLevels = (await levels.ListAllTrackedAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var newId = Guid.CreateVersion7();

        int order;
        Guid? nextLevelId;

        if (request.InsertAfterLevelId is not null)
        {
            var afterId = Guid.Parse(request.InsertAfterLevelId);
            var after = allLevels.FirstOrDefault(level => level.Id == afterId && level.Status == LevelStatus.Active);

            if (after is null)
            {
                return Result.Failure<LevelDto>(Error.Validation(
                    "level.insert_after_not_found", "No active level was found with that id to insert after."));
            }

            order = after.ProgressionOrder + 1;
            nextLevelId = after.NextLevelId;

            // Spec 6.4.2 worked case: every later active level shifts up by one, in the same transaction.
            var toShift = allLevels
                .Where(level => level.Status == LevelStatus.Active && level.Id != after.Id && level.ProgressionOrder >= order)
                .ToArray();

            // See IClassLevelRepository.NegateProgressionOrdersAsync's remarks: shifting several rows'
            // progression_order up by one, in place, risks the same mid-batch partial-unique-index
            // collision reorder does (proven empirically) — negate first, then increment as normal;
            // each entity's in-memory ProgressionOrder is untouched by the raw statement, so the +1
            // below still computes off the real original value.
            await levels.NegateProgressionOrdersAsync(
                toShift.Select(level => level.Id).ToArray(), cancellationToken).ConfigureAwait(false);

            foreach (var level in toShift)
            {
                level.SetProgressionOrder(level.ProgressionOrder + 1);
            }

            var pointAfterAtNew = after.SetNextLevel(newId);

            if (pointAfterAtNew.IsFailure)
            {
                return Result.Failure<LevelDto>(pointAfterAtNew.Error);
            }
        }
        else
        {
            order = request.ProgressionOrder!.Value;
            nextLevelId = request.NextLevelId is null ? null : Guid.Parse(request.NextLevelId);
        }

        var creation = ClassLevel.Create(newId, trimmedName, sectionId, order, nextLevelId);

        if (creation.IsFailure)
        {
            return Result.Failure<LevelDto>(creation.Error);
        }

        var newLevel = creation.Value;
        allLevels.Add(newLevel);

        var namesById = allLevels.ToDictionary(level => level.Id, level => level.Name);
        var chainCheck = ProgressionChainGuard.Validate(LevelMapper.ToChainLevels(allLevels), namesById);

        if (chainCheck.IsFailure)
        {
            return Result.Failure<LevelDto>(chainCheck.Error);
        }

        await levels.AddAsync(newLevel, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Level.Create,
            EntityType,
            newLevel.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        var sectionNamesById = allSections.ToDictionary(section => section.Id, section => section.Name);
        return Result.Success(LevelMapper.ToDto(newLevel, allLevels, sectionNamesById));
    }
}
