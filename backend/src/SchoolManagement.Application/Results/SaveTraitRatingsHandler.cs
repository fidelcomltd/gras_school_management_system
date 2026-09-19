using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="SaveTraitRatingsCommand"/>.</summary>
/// <remarks>
/// <c>result.trait.enter</c> is the route's ONE declarative privilege (arm-scoped) — spec 6.7.2 also
/// requires the result set to be Draft or Returned for Correction and the arm's section to rate
/// traits (ruling R1), both DATA-DEPENDENT, so both are enforced here, same "route declares the
/// baseline, handler enforces the data-dependent rest" split <c>SaveScoreSheetHandler</c> uses.
/// Ratings are NEVER converted to marks and never computed (spec §6.7.12 amendment: "Development
/// ratings and trait ratings are never converted to marks, never averaged, never ranked") — unlike a
/// score save, this handler never calls <see cref="ResultSet.MarkNeedsRecompute"/> on an EXISTING
/// result set; a brand-new one still opens with <c>needs_recompute</c> true only because
/// <see cref="ResultSet.Create"/> itself always does, not because a rating triggered it.
/// </remarks>
internal sealed class SaveTraitRatingsHandler(
    IArmRepository arms,
    IAcademicSessionRepository sessions,
    ITermRepository terms,
    IClassLevelRepository classLevels,
    ISectionRepository sections,
    IEnrolmentRepository enrolments,
    ITraitRepository traits,
    IRatingScaleRepository ratingScales,
    IResultSetRepository resultSets,
    ITraitRatingRepository traitRatings,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<SaveTraitRatingsCommand, Result<TraitRatingSheetDto>>
{
    /// <summary>The result set exists and is not Draft or Returned for Correction (spec 6.7.2).</summary>
    public const string ResultSetLockedErrorCode = "trait_ratings.result_set_locked";

    /// <summary>The submitted <c>version</c> does not match the grid's current version.</summary>
    public const string StaleVersionErrorCode = "trait_ratings.stale_version";

    /// <inheritdoc />
    public async Task<Result<TraitRatingSheetDto>> HandleAsync(SaveTraitRatingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var armId = Guid.Parse(request.ArmId);
        var termId = Guid.Parse(request.TermId);

        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<TraitRatingSheetDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<TraitRatingSheetDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        if (term.SessionId != arm.SessionId)
        {
            return Result.Failure<TraitRatingSheetDto>(Error.Validation(
                "trait_ratings.term_session_mismatch", "The term must belong to the arm's session."));
        }

        var sectionCheck = await GetTraitRatingsHandler.ResolveSectionAsync(arm.ClassLevelId, classLevels, sections, cancellationToken)
            .ConfigureAwait(false);
        if (sectionCheck.IsFailure)
        {
            return Result.Failure<TraitRatingSheetDto>(sectionCheck.Error);
        }

        var session = await sessions.FindReadOnlyByIdAsync(arm.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is { State: SessionState.Closed })
        {
            return Result.Failure<TraitRatingSheetDto>(Error.Conflict(
                "trait_ratings.session_closed", "This arm's session is closed. Ratings cannot be entered."));
        }

        if (term.State == TermState.Closed)
        {
            return Result.Failure<TraitRatingSheetDto>(Error.Conflict(
                "trait_ratings.term_closed", $"{term.Name} is closed. Ratings cannot be entered."));
        }

        var roster = await enrolments.ListActiveRosterByArmAsync(armId, cancellationToken).ConfigureAwait(false);
        var rosterPupilIds = roster.Select(pupil => pupil.PupilId).ToHashSet();

        var allTraits = await traits.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var activeTraitsById = allTraits.Where(trait => trait.Status == TraitStatus.Active).ToDictionary(trait => trait.Id);
        var allTraitsById = allTraits.ToDictionary(trait => trait.Id);

        var blocks = await traits.ListBlocksReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var blocksByDomain = blocks.ToDictionary(block => block.Id);

        var scales = await ratingScales.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var scalesById = scales.ToDictionary(scale => scale.Id);

        var failures = ValidateRows(request.Rows, rosterPupilIds, activeTraitsById, allTraitsById, blocksByDomain, scalesById);
        if (failures.Count > 0)
        {
            return Result.Failure<TraitRatingSheetDto>(new ValidationError(failures));
        }

        var existingResultSet = await resultSets.FindTrackedByArmTermAsync(armId, termId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TraitRating> existingRatings = existingResultSet is null
            ? []
            : await traitRatings.ListTrackedAsync(existingResultSet.Id, cancellationToken).ConfigureAwait(false);

        var currentVersion = TraitRatingVersion.Compute(existingRatings
            .Select(rating => new TraitRatingSnapshot(rating.PupilId, rating.TraitId, rating.RatingScalePointId))
            .ToArray());

        // Locked-state check runs BEFORE the staleness check — same ordering rule as
        // SaveScoreSheetHandler, and for the same reason: a caller who cannot edit this result set at
        // all must be told that first, not sent chasing a version conflict.
        if (existingResultSet is not null &&
            existingResultSet.State is not (ResultSetState.Draft or ResultSetState.ReturnedForCorrection))
        {
            return Result.Failure<TraitRatingSheetDto>(Error.Conflict(
                ResultSetLockedErrorCode, $"This result set is {existingResultSet.State} and ratings cannot be edited."));
        }

        if (!string.Equals(request.Version, currentVersion, StringComparison.Ordinal))
        {
            return Result.Failure<TraitRatingSheetDto>(Error.Conflict(
                StaleVersionErrorCode, "This grid was changed since you last read it. Reload it before saving again."));
        }

        var byPupilTrait = existingRatings.ToDictionary(rating => (rating.PupilId, rating.TraitId));
        var resultSetRef = existingResultSet;

        var beforeChanges = new List<object?>();
        var afterChanges = new List<object?>();

        foreach (var row in request.Rows)
        {
            var pupilId = Guid.Parse(row.PupilId);

            foreach (var (key, value) in row.Ratings!)
            {
                var traitId = Guid.Parse(key);
                var cellKey = (pupilId, traitId);
                var hasExisting = byPupilTrait.TryGetValue(cellKey, out var existing);

                if (value is null)
                {
                    if (hasExisting)
                    {
                        beforeChanges.Add(Snapshot(pupilId, traitId, existing!.RatingScalePointId));
                        afterChanges.Add(Snapshot(pupilId, traitId, pointId: null));
                        await traitRatings.RemoveAsync(existing!, cancellationToken).ConfigureAwait(false);
                        byPupilTrait.Remove(cellKey);
                    }

                    continue;
                }

                var pointId = Guid.Parse(value);

                if (hasExisting)
                {
                    beforeChanges.Add(Snapshot(pupilId, traitId, existing!.RatingScalePointId));
                    existing.UpdatePoint(pointId);
                }
                else
                {
                    beforeChanges.Add(Snapshot(pupilId, traitId, pointId: null));

                    if (resultSetRef is null)
                    {
                        var creation = ResultSet.Create(Guid.CreateVersion7(), armId, termId);
                        if (creation.IsFailure)
                        {
                            return Result.Failure<TraitRatingSheetDto>(creation.Error);
                        }

                        resultSetRef = creation.Value;
                        await resultSets.AddAsync(resultSetRef, cancellationToken).ConfigureAwait(false);
                    }

                    var ratingCreation = TraitRating.Create(Guid.CreateVersion7(), resultSetRef.Id, pupilId, traitId, pointId);
                    if (ratingCreation.IsFailure)
                    {
                        return Result.Failure<TraitRatingSheetDto>(ratingCreation.Error);
                    }

                    existing = ratingCreation.Value;
                    await traitRatings.AddAsync(existing, cancellationToken).ConfigureAwait(false);
                    byPupilTrait[cellKey] = existing;
                }

                afterChanges.Add(Snapshot(pupilId, traitId, pointId));
            }
        }

        if (afterChanges.Count > 0)
        {
            await auditSink.RecordAsync(
                Privileges.Results.TraitEnter,
                "trait_rating",
                entityId: null,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["armId"] = request.ArmId, ["termId"] = request.TermId, ["changes"] = afterChanges },
                currentUser.UserId,
                cancellationToken,
                reason: null,
                beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["armId"] = request.ArmId, ["termId"] = request.TermId, ["changes"] = beforeChanges })
                .ConfigureAwait(false);
        }

        var finalRatings = byPupilTrait.Values
            .Select(rating => new TraitRatingSnapshot(rating.PupilId, rating.TraitId, rating.RatingScalePointId))
            .ToArray();

        var dto = TraitRatingProjection.Build(armId, termId, resultSetRef, roster, allTraits, blocks, scales, finalRatings);

        return Result.Success(dto);
    }

    private static Dictionary<string, object?> Snapshot(Guid pupilId, Guid traitId, Guid? pointId) => new(StringComparer.Ordinal)
    {
        ["pupilId"] = pupilId.ToString("D", CultureInfo.InvariantCulture),
        ["traitId"] = traitId.ToString("D", CultureInfo.InvariantCulture),
        ["pointId"] = pointId?.ToString("D", CultureInfo.InvariantCulture),
    };

    private static Dictionary<string, string[]> ValidateRows(
        IReadOnlyList<SaveTraitRatingsRowInput> rows,
        HashSet<Guid> rosterPupilIds,
        Dictionary<Guid, Trait> activeTraitsById,
        Dictionary<Guid, Trait> allTraitsById,
        Dictionary<TraitDomain, TraitBlock> blocksByDomain,
        Dictionary<Guid, RatingScale> scalesById)
    {
        var failures = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void AddFailure(string path, string message)
        {
            if (!failures.TryGetValue(path, out var list))
            {
                list = [];
                failures[path] = list;
            }

            if (!list.Contains(message, StringComparer.Ordinal))
            {
                list.Add(message);
            }
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];

            if (!Guid.TryParse(row.PupilId, out var pupilId) || !rosterPupilIds.Contains(pupilId))
            {
                AddFailure($"Rows[{index}].PupilId", "This pupil is not on the arm's active roster.");
                continue;
            }

            foreach (var (key, value) in row.Ratings ?? new Dictionary<string, string?>(StringComparer.Ordinal))
            {
                if (!Guid.TryParse(key, out var traitId))
                {
                    AddFailure($"Rows[{index}].Ratings[{key}]", "This is not a valid trait id.");
                    continue;
                }

                if (!activeTraitsById.TryGetValue(traitId, out var trait))
                {
                    AddFailure(
                        $"Rows[{index}].Ratings[{key}]",
                        allTraitsById.ContainsKey(traitId) ? "This trait is archived and cannot be rated." : "This is not a known trait.");
                    continue;
                }

                if (value is null)
                {
                    continue;
                }

                if (!Guid.TryParse(value, out var pointId))
                {
                    AddFailure($"Rows[{index}].Ratings[{key}]", "This is not a valid point id.");
                    continue;
                }

                var block = blocksByDomain[trait.Domain];
                var scale = scalesById[block.RatingScaleId];
                if (!scale.Points.Any(point => point.Id == pointId))
                {
                    AddFailure($"Rows[{index}].Ratings[{key}]", $"This point is not on {scale.Name}.");
                }
            }
        }

        return failures.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal);
    }
}
