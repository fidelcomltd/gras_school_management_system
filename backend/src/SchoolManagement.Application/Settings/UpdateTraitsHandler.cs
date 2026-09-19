using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="UpdateTraitsCommand"/>.</summary>
/// <remarks>
/// <para>
/// OPTIMISTIC CONCURRENCY, same pattern as every other settings group:
/// <see cref="UpdateTraitsCommand.ExpectedVersion"/> is compared against
/// <see cref="SchoolProfile.TraitsVersionNumber"/> BEFORE anything else runs.
/// </para>
/// <para>
/// TRAITS ARE NOT SECTION-SCOPED (human decision, TASK-0072 stage 3) — unlike
/// <see cref="DevelopmentDomain"/>, a <see cref="Trait"/> carries only a <see cref="TraitDomain"/>
/// (affective or psychomotor), never a section id, so there is no section-id validation here.
/// </para>
/// <para>
/// IDS ARE STABLE ACROSS A SAVE, from the start — a submitted trait carrying an <c>id</c> is updated
/// in place, id preserved; one with no <c>id</c> is new. A submitted id that matches no current trait
/// is rejected <c>422 settings.traits.unknown_trait_id</c>, naming the offending position.
/// </para>
/// <para>
/// REMOVAL VS ARCHIVE (spec 6.2.7, carried into 6.2.13's per-block scales): an EXISTING trait whose id
/// is absent from the whole submission is being REMOVED — walked through <see cref="ITraitUsageGate"/>
/// before anything is written, refused <c>409 settings.traits.trait_rated</c> on the first trait that
/// has ever been rated. An id that IS present, even with a changed <c>status</c>, is never removed and
/// so never gated — archiving a submitted id is always allowed.
/// </para>
/// <para>
/// <see cref="UpdateTraitsCommand.AffectiveRatingScaleId"/> and
/// <see cref="UpdateTraitsCommand.PsychomotorRatingScaleId"/> must each match an existing
/// <see cref="RatingScale"/>, rejected <c>422 settings.traits.unknown_scale_id</c> otherwise —
/// matching <c>UpdateDevelopmentDomainsCommandHandler</c>'s own <c>ratingScaleId</c> check.
/// </para>
/// <para>
/// THE SNAPSHOT CARRIES EVERY GROUP, NOT ONLY THE ONE THAT CHANGED (spec 6.2.9) — TASK-0072 stage 3a's
/// <see cref="ISettingsSnapshotSource"/> loads every OTHER group's current state in one call; this
/// handler overrides only <see cref="SettingsSnapshotState.Traits"/> and
/// <see cref="SettingsSnapshotState.TraitBlocks"/> with the just-saved, pre-commit state before
/// handing the result to <see cref="SettingsSnapshotBuilder.Build"/> — traits are the FIRST group
/// built entirely under the stage 3a seam, touching no other handler.
/// </para>
/// </remarks>
internal sealed class UpdateTraitsCommandHandler(
    ISchoolProfileRepository schoolProfileRepository,
    ITraitRepository traitRepository,
    IRatingScaleRepository ratingScaleRepository,
    IConfigVersionRepository configVersionRepository,
    IAcademicSessionRepository academicSessionRepository,
    IPublishedResultsGate publishedResultsGate,
    ITraitUsageGate traitUsageGate,
    ISettingsSnapshotSource settingsSnapshotSource,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateTraitsCommand, Result<SettingsTraitsGroupDto>>
{
    /// <summary>Stable error code for a stale <see cref="SchoolProfile.TraitsVersionNumber"/> save.</summary>
    public const string StaleVersionErrorCode = "settings.traits.stale_version";

    /// <summary>Stable error code for an <c>affectiveRatingScaleId</c>/<c>psychomotorRatingScaleId</c> that matches no existing scale.</summary>
    public const string UnknownScaleIdErrorCode = "settings.traits.unknown_scale_id";

    /// <summary>Stable error code for a submitted trait id that matches no current trait.</summary>
    public const string UnknownTraitIdErrorCode = "settings.traits.unknown_trait_id";

    /// <summary>Stable error code for removing a trait that has ever been rated (spec 6.2.7's exact message).</summary>
    public const string TraitRatedErrorCode = "settings.traits.trait_rated";

    /// <summary>Stable error code for changing a block's rating scale while an open result set holds a rating under the old one (TASK-0083 stage 3, R2).</summary>
    public const string ScaleChangedWhileRatedErrorCode = "settings.traits.scale_changed_while_rated";

    private const string SchoolProfileEntityType = "school_profile";

    /// <inheritdoc />
    public async Task<Result<SettingsTraitsGroupDto>> HandleAsync(UpdateTraitsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await schoolProfileRepository
            .GetTrackedSingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var profileIdText = profile.Id.ToString("D", CultureInfo.InvariantCulture);

        if (profile.TraitsVersionNumber != request.ExpectedVersion)
        {
            await auditSink.RecordRejectionAsync(
                "settings.traits.save_rejected_stale_version",
                SchoolProfileEntityType,
                profileIdText,
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["expectedVersion"] = request.ExpectedVersion,
                    ["currentVersion"] = profile.TraitsVersionNumber,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<SettingsTraitsGroupDto>(Error.Conflict(
                StaleVersionErrorCode,
                "The traits were changed by another administrator while you were editing. Reload and " +
                "make your change again."));
        }

        var existingTraits = await traitRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var existingTraitBlocks = await traitRepository.ListBlocksReadOnlyAsync(cancellationToken).ConfigureAwait(false);

        // R2 (scale_changed_while_rated), stage 3: BEFORE the reason gate, unknown-scale-id checks,
        // structural validation or the existing trait-removal gate — a block whose scale is CHANGING
        // is refused while any open (not Published) result set holds a rating, on the OLD scale, for
        // one of ITS traits. A block whose scale is unchanged is never asked.
        var scaleChangedResult = await RefuseIfBlockScaleChangedWhileRatedAsync(
                request, existingTraits, existingTraitBlocks, cancellationToken)
            .ConfigureAwait(false);
        if (scaleChangedResult is not null)
        {
            return scaleChangedResult;
        }

        var reasonCheck = await PublishedResultsReasonGate
            .RequireReasonIfPublishedAsync(request.Reason, academicSessionRepository, publishedResultsGate, cancellationToken)
            .ConfigureAwait(false);
        if (reasonCheck.IsFailure)
        {
            return Result.Failure<SettingsTraitsGroupDto>(reasonCheck.Error);
        }

        var existingScales = await ratingScaleRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var existingScaleIds = existingScales.Select(scale => scale.Id).ToHashSet();

        if (!existingScaleIds.Contains(request.AffectiveRatingScaleId))
        {
            return Result.Failure<SettingsTraitsGroupDto>(Error.Validation(
                UnknownScaleIdErrorCode,
                "affectiveRatingScaleId does not match any existing rating scale. Reload and try again."));
        }

        if (!existingScaleIds.Contains(request.PsychomotorRatingScaleId))
        {
            return Result.Failure<SettingsTraitsGroupDto>(Error.Validation(
                UnknownScaleIdErrorCode,
                "psychomotorRatingScaleId does not match any existing rating scale. Reload and try again."));
        }

        var structuralValidation = TraitRules.ValidateWholeSet(request.Traits);
        if (structuralValidation.IsFailure)
        {
            return Result.Failure<SettingsTraitsGroupDto>(structuralValidation.Error);
        }

        var existingTraitsById = existingTraits.ToDictionary(trait => trait.Id);
        var submittedTraitIds = request.Traits
            .Where(trait => trait.Id is not null)
            .Select(trait => trait.Id!.Value)
            .ToHashSet();

        // Unknown ids first — a submitted id that matches nothing live, 422, names the offending position.
        for (var index = 0; index < request.Traits.Count; index++)
        {
            var traitInput = request.Traits[index];
            if (traitInput.Id is Guid submittedId && !existingTraitsById.ContainsKey(submittedId))
            {
                return Result.Failure<SettingsTraitsGroupDto>(new TraitValidationError(
                    UnknownTraitIdErrorCode,
                    $"Trait {traitInput.Name} has an id that does not match any existing trait. Reload and try again.",
                    index));
            }
        }

        // An EXISTING trait whose id is absent from the whole submitted set is being REMOVED. An id
        // that IS present (even with a changed status) is archiving, never gated — see this type's remarks.
        foreach (var existingTrait in existingTraits)
        {
            if (submittedTraitIds.Contains(existingTrait.Id))
            {
                continue;
            }

            var everRated = await traitUsageGate
                .HasEverBeenRatedAsync(existingTrait.Id, cancellationToken)
                .ConfigureAwait(false);

            if (!everRated)
            {
                continue;
            }

            await auditSink.RecordRejectionAsync(
                "settings.traits.save_rejected_trait_rated",
                "trait",
                existingTrait.Id.ToString("D", CultureInfo.InvariantCulture),
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["traitName"] = existingTrait.Name },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<SettingsTraitsGroupDto>(Error.Conflict(
                TraitRatedErrorCode,
                $"Ratings have already been entered for {existingTrait.Name} this term. Archive the " +
                "trait instead, which keeps it on this term's sheets and removes it from next term."));
        }

        var traits = request.Traits
            .Select(traitInput => Trait.Create(
                traitInput.Id ?? Guid.CreateVersion7(),
                traitInput.Domain,
                traitInput.Name,
                traitInput.DisplayOrder,
                traitInput.Status))
            .ToList();

        await traitRepository
            .ReplaceAllAsync(traits, request.AffectiveRatingScaleId, request.PsychomotorRatingScaleId, cancellationToken)
            .ConfigureAwait(false);

        profile.IncrementTraitsVersion();

        var traitBlocks = new List<TraitBlock>
        {
            TraitBlock.Create(TraitDomain.Affective, request.AffectiveRatingScaleId),
            TraitBlock.Create(TraitDomain.Psychomotor, request.PsychomotorRatingScaleId),
        };

        var snapshotState = await settingsSnapshotSource.LoadAsync(cancellationToken).ConfigureAwait(false);

        var configVersion = ConfigVersion.Create(
            Guid.CreateVersion7(),
            SettingsSnapshotBuilder.Build(profile, snapshotState with { Traits = traits, TraitBlocks = traitBlocks }),
            ConfigVersionGroup.Traits,
            currentUser.UserId,
            reason: reasonCheck.Value,
            now);

        await configVersionRepository.AddAsync(configVersion, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            "settings.traits.updated",
            SchoolProfileEntityType,
            profileIdText,
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["traitCount"] = traits.Count },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SettingsMapper.ToTraitsDto(
            traits,
            request.AffectiveRatingScaleId,
            request.PsychomotorRatingScaleId,
            profile.TraitsVersionNumber));
    }

    /// <summary>
    /// Walks both <paramref name="existingTraitBlocks"/> and refuses on the FIRST one whose scale
    /// <paramref name="request"/> is CHANGING (its submitted <c>affectiveRatingScaleId</c>/
    /// <c>psychomotorRatingScaleId</c> differs from the block's current <see cref="TraitBlock.RatingScaleId"/>)
    /// while that block still has an open (not Published) rating under its OLD scale — R2, TASK-0083
    /// stage 3. A block whose scale is unchanged is never asked. Returns <see langword="null"/> when
    /// nothing is refused.
    /// </summary>
    private async Task<Result<SettingsTraitsGroupDto>?> RefuseIfBlockScaleChangedWhileRatedAsync(
        UpdateTraitsCommand request,
        IReadOnlyList<Trait> existingTraits,
        IReadOnlyList<TraitBlock> existingTraitBlocks,
        CancellationToken cancellationToken)
    {
        foreach (var block in existingTraitBlocks)
        {
            var submittedScaleId = block.Id == TraitDomain.Affective
                ? request.AffectiveRatingScaleId
                : request.PsychomotorRatingScaleId;

            if (submittedScaleId == block.RatingScaleId)
            {
                continue;
            }

            var traitIds = existingTraits
                .Where(trait => trait.Domain == block.Id)
                .Select(trait => trait.Id)
                .ToList();

            var openRating = await traitUsageGate
                .HasOpenRatingOnScaleAsync(traitIds, block.RatingScaleId, cancellationToken)
                .ConfigureAwait(false);
            if (!openRating)
            {
                continue;
            }

            await auditSink.RecordRejectionAsync(
                "settings.traits.save_rejected_scale_changed_while_rated",
                "trait_block",
                block.Id.ToString(),
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["domain"] = block.Id.ToString() },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<SettingsTraitsGroupDto>(Error.Conflict(
                ScaleChangedWhileRatedErrorCode,
                $"Ratings have already been entered for the {block.Id} block this term. Change its " +
                "rating scale only after those result sets are approved and published, or leave the " +
                "scale as it is."));
        }

        return null;
    }
}
