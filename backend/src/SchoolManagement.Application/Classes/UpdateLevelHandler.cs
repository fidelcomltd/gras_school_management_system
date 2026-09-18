using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// Handles <see cref="UpdateLevelCommand"/>. The route requires only <c>level.update</c> — a
/// <see cref="UpdateLevelCommand.Status"/> change ADDITIONALLY requires <c>level.deactivate</c>,
/// checked here because it is data-dependent (whether the body touches <c>status</c> at all), the
/// same shape <c>ChangeAdminAccountStatusCommandHandler</c> uses for its own data-dependent privilege.
/// </summary>
internal sealed class UpdateLevelHandler(
    IClassLevelRepository levels,
    ISectionRepository sections,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<UpdateLevelCommand, Result<LevelDto>>
{
    private const string EntityType = "class_level";

    /// <inheritdoc />
    public async Task<Result<LevelDto>> HandleAsync(UpdateLevelCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allLevels = (await levels.ListAllTrackedAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var level = allLevels.FirstOrDefault(candidate => candidate.Id == request.Id);

        if (level is null)
        {
            return Result.Failure<LevelDto>(Error.NotFound("level.not_found", "No class level was found with that id."));
        }

        if (request.Name is not null)
        {
            var trimmedKey = request.Name.Trim().ToLowerInvariant();

            if (!string.Equals(trimmedKey, level.NameKey, StringComparison.Ordinal) &&
                allLevels.Any(other => other.Id != level.Id && other.NameKey == trimmedKey))
            {
                return Result.Failure<LevelDto>(Error.Conflict(
                    "level.name_duplicate", $"A class level named {request.Name.Trim()} already exists."));
            }

            var rename = level.Rename(request.Name);

            if (rename.IsFailure)
            {
                return Result.Failure<LevelDto>(rename.Error);
            }
        }

        var allSections = await sections.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);

        if (request.SectionId is not null)
        {
            var sectionId = Guid.Parse(request.SectionId);

            if (allSections.All(section => section.Id != sectionId))
            {
                return Result.Failure<LevelDto>(Error.Validation(
                    "level.section_not_found", "No section was found with that id."));
            }

            level.ChangeSection(sectionId);
        }

        if (request.NextLevelId is not null)
        {
            var nextLevelId = request.NextLevelId.Length == 0 ? (Guid?)null : Guid.Parse(request.NextLevelId);
            var setNext = level.SetNextLevel(nextLevelId);

            if (setNext.IsFailure)
            {
                return Result.Failure<LevelDto>(setNext.Error);
            }
        }

        if (request.ProgressionOrder is { } order)
        {
            level.SetProgressionOrder(order);
        }

        if (request.Status is { } status && status != level.Status)
        {
            if (currentUser.UserId is not { } actorId)
            {
                return Result.Failure<LevelDto>(Error.Unauthenticated(
                    "authentication.required", "Sign in to perform this action."));
            }

            var grants = await effectivePrivilegeProvider.GetGrantsAsync(actorId, cancellationToken).ConfigureAwait(false);

            if (!grants.Any(grant => grant.Privilege == Privileges.Level.Deactivate))
            {
                return Result.Failure<LevelDto>(Error.Forbidden(
                    "level.deactivate_denied", "You do not have permission to change this level's status."));
            }

            if (status == LevelStatus.Active)
            {
                level.Activate();
            }
            else
            {
                // Spec 6.4.2's dedicated deactivation precondition — checked BEFORE mutating status,
                // over the set as it stands right now (still including `level` itself, still Active).
                // See ProgressionChainGuard.CanDeactivate's remarks for why this is a separate check
                // from the generic Validate() re-run below, not a substitute for it.
                var activeBeforeDeactivation = LevelMapper.ToChainLevels(allLevels);
                var target = activeBeforeDeactivation.First(candidate => candidate.Id == level.Id);
                var deactivationCheck = ProgressionChainGuard.CanDeactivate(target, activeBeforeDeactivation);

                if (deactivationCheck.IsFailure)
                {
                    return Result.Failure<LevelDto>(deactivationCheck.Error);
                }

                level.Deactivate();
            }
        }

        var namesById = allLevels.ToDictionary(candidate => candidate.Id, candidate => candidate.Name);
        var chainCheck = ProgressionChainGuard.Validate(LevelMapper.ToChainLevels(allLevels), namesById);

        if (chainCheck.IsFailure)
        {
            return Result.Failure<LevelDto>(chainCheck.Error);
        }

        await auditSink.RecordAsync(
            Privileges.Level.Update,
            EntityType,
            level.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        var sectionNamesById = allSections.ToDictionary(section => section.Id, section => section.Name);
        return Result.Success(LevelMapper.ToDto(level, allLevels, sectionNamesById));
    }
}
