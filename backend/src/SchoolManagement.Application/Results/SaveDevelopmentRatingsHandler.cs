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

/// <summary>Handles <see cref="SaveDevelopmentRatingsCommand"/>.</summary>
/// <remarks>
/// <para>
/// <c>result.trait.enter</c> is the route's ONE declarative privilege (arm-scoped), reused for the
/// nursery sheet per ruling R3 — spec names no separate privilege for development ratings. Spec 6.7.2
/// also requires the result set to be Draft or Returned for Correction and the arm's section to rate
/// development domains (ruling R1), both DATA-DEPENDENT, so both are enforced here, same
/// "route declares the baseline, handler enforces the data-dependent rest" split
/// <c>SaveTraitRatingsHandler</c>/<c>SaveScoreSheetHandler</c> use. Ratings are NEVER converted to
/// marks and never computed (spec §6.7.12 amendment) — like a trait save, this handler never calls
/// <see cref="ResultSet.MarkNeedsRecompute"/> on an EXISTING result set.
/// </para>
/// <para>
/// PER-CELL VALIDATION, ONE PASS: unknown/archived indicator, a point not on the indicator's domain's
/// scale, a pupil not on the roster, a comment without a point, a comment over 120 characters, and a
/// comment on a domain where <c>allowsIndicatorComment</c> is false are ALL folded into one
/// <see cref="ValidationError"/> (<c>request.validation_failed</c>) — the same "per-cell errors fold
/// together" convention <c>SaveTraitRatingsHandler.ValidateRows</c> established, extended here to the
/// richer cell shape (point AND comment) nursery ratings need. R1's section gate is a PRECONDITION on
/// the whole request, checked before any row is touched, so it stays its own top-level 422 — exactly
/// like <c>trait_ratings.section_not_rated</c>.
/// </para>
/// </remarks>
internal sealed class SaveDevelopmentRatingsHandler(
    IArmRepository arms,
    IAcademicSessionRepository sessions,
    ITermRepository terms,
    IClassLevelRepository classLevels,
    ISectionRepository sections,
    IEnrolmentRepository enrolments,
    IDevelopmentDomainRepository developmentDomains,
    IRatingScaleRepository ratingScales,
    IResultSetRepository resultSets,
    IDevelopmentRatingRepository developmentRatings,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<SaveDevelopmentRatingsCommand, Result<DevelopmentRatingSheetDto>>
{
    /// <summary>The result set exists and is not Draft or Returned for Correction (spec 6.7.2).</summary>
    public const string ResultSetLockedErrorCode = "development_ratings.result_set_locked";

    /// <summary>The submitted <c>version</c> does not match the grid's current version.</summary>
    public const string StaleVersionErrorCode = "development_ratings.stale_version";

    /// <inheritdoc />
    public async Task<Result<DevelopmentRatingSheetDto>> HandleAsync(SaveDevelopmentRatingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var armId = Guid.Parse(request.ArmId);
        var termId = Guid.Parse(request.TermId);

        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<DevelopmentRatingSheetDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<DevelopmentRatingSheetDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        if (term.SessionId != arm.SessionId)
        {
            return Result.Failure<DevelopmentRatingSheetDto>(Error.Validation(
                "development_ratings.term_session_mismatch", "The term must belong to the arm's session."));
        }

        var sectionCheck = await GetDevelopmentRatingsHandler.ResolveSectionAsync(
            arm.ClassLevelId, classLevels, sections, developmentDomains, cancellationToken).ConfigureAwait(false);
        if (sectionCheck.IsFailure)
        {
            return Result.Failure<DevelopmentRatingSheetDto>(sectionCheck.Error);
        }

        var session = await sessions.FindReadOnlyByIdAsync(arm.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is { State: SessionState.Closed })
        {
            return Result.Failure<DevelopmentRatingSheetDto>(Error.Conflict(
                "development_ratings.session_closed", "This arm's session is closed. Ratings cannot be entered."));
        }

        if (term.State == TermState.Closed)
        {
            return Result.Failure<DevelopmentRatingSheetDto>(Error.Conflict(
                "development_ratings.term_closed", $"{term.Name} is closed. Ratings cannot be entered."));
        }

        var roster = await enrolments.ListActiveRosterByArmAsync(armId, cancellationToken).ConfigureAwait(false);
        var rosterPupilIds = roster.Select(pupil => pupil.PupilId).ToHashSet();

        // Every indicator of this SECTION, active or archived, each paired with its owning domain —
        // needed to tell "unknown" from "archived" and to look up the domain's scale/comment rule.
        var indicatorsByDomain = sectionCheck.Value.AllDomainsOfSection
            .SelectMany(domain => domain.Indicators.Select(indicator => (Indicator: indicator, Domain: domain)))
            .ToDictionary(entry => entry.Indicator.Id);

        var scales = await ratingScales.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var scalesById = scales.ToDictionary(scale => scale.Id);

        var failures = ValidateRows(request.Rows, rosterPupilIds, indicatorsByDomain, scalesById);
        if (failures.Count > 0)
        {
            return Result.Failure<DevelopmentRatingSheetDto>(new ValidationError(failures));
        }

        // TASK-0088 AC A4: row-locked before the state check below.
        var existingResultSet = await resultSets.FindTrackedByArmTermForUpdateAsync(armId, termId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<DevelopmentRating> existingRatings = existingResultSet is null
            ? []
            : await developmentRatings.ListTrackedAsync(existingResultSet.Id, cancellationToken).ConfigureAwait(false);

        var currentVersion = DevelopmentRatingVersion.Compute(existingRatings
            .Select(rating => new DevelopmentRatingSnapshot(rating.PupilId, rating.IndicatorId, rating.RatingScalePointId, rating.Comment))
            .ToArray());

        // Locked-state check runs BEFORE the staleness check — same ordering rule as
        // SaveTraitRatingsHandler/SaveScoreSheetHandler.
        if (existingResultSet is not null &&
            existingResultSet.State is not (ResultSetState.Draft or ResultSetState.ReturnedForCorrection))
        {
            return Result.Failure<DevelopmentRatingSheetDto>(Error.Conflict(
                ResultSetLockedErrorCode, $"This result set is {existingResultSet.State} and ratings cannot be edited."));
        }

        if (!string.Equals(request.Version, currentVersion, StringComparison.Ordinal))
        {
            return Result.Failure<DevelopmentRatingSheetDto>(Error.Conflict(
                StaleVersionErrorCode, "This grid was changed since you last read it. Reload it before saving again."));
        }

        var byPupilIndicator = existingRatings.ToDictionary(rating => (rating.PupilId, rating.IndicatorId));
        var resultSetRef = existingResultSet;

        var beforeChanges = new List<object?>();
        var afterChanges = new List<object?>();

        foreach (var row in request.Rows)
        {
            var pupilId = Guid.Parse(row.PupilId);

            foreach (var (key, cell) in row.Ratings!)
            {
                var indicatorId = Guid.Parse(key);
                var cellKey = (pupilId, indicatorId);
                var hasExisting = byPupilIndicator.TryGetValue(cellKey, out var existing);

                // A whole-null value AND an object with a null PointId both mean "clear" (Q1-A / Q3-A).
                if (cell is null || cell.PointId is null)
                {
                    if (hasExisting)
                    {
                        beforeChanges.Add(Snapshot(pupilId, indicatorId, existing!.RatingScalePointId, existing.Comment));
                        afterChanges.Add(Snapshot(pupilId, indicatorId, pointId: null, comment: null));
                        await developmentRatings.RemoveAsync(existing!, cancellationToken).ConfigureAwait(false);
                        byPupilIndicator.Remove(cellKey);
                    }

                    continue;
                }

                var pointId = Guid.Parse(cell.PointId);
                var comment = string.IsNullOrWhiteSpace(cell.Comment) ? null : cell.Comment.Trim();

                if (hasExisting)
                {
                    beforeChanges.Add(Snapshot(pupilId, indicatorId, existing!.RatingScalePointId, existing.Comment));
                    existing.UpdateRating(pointId, comment);
                }
                else
                {
                    beforeChanges.Add(Snapshot(pupilId, indicatorId, pointId: null, comment: null));

                    if (resultSetRef is null)
                    {
                        var creation = ResultSet.Create(Guid.CreateVersion7(), armId, termId);
                        if (creation.IsFailure)
                        {
                            return Result.Failure<DevelopmentRatingSheetDto>(creation.Error);
                        }

                        resultSetRef = creation.Value;
                        await resultSets.AddAsync(resultSetRef, cancellationToken).ConfigureAwait(false);
                    }

                    var ratingCreation = DevelopmentRating.Create(Guid.CreateVersion7(), resultSetRef.Id, pupilId, indicatorId, pointId, comment);
                    if (ratingCreation.IsFailure)
                    {
                        return Result.Failure<DevelopmentRatingSheetDto>(ratingCreation.Error);
                    }

                    existing = ratingCreation.Value;
                    await developmentRatings.AddAsync(existing, cancellationToken).ConfigureAwait(false);
                    byPupilIndicator[cellKey] = existing;
                }

                afterChanges.Add(Snapshot(pupilId, indicatorId, pointId, comment));
            }
        }

        if (afterChanges.Count > 0)
        {
            await auditSink.RecordAsync(
                Privileges.Results.TraitEnter,
                "development_rating",
                entityId: null,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["armId"] = request.ArmId, ["termId"] = request.TermId, ["changes"] = afterChanges },
                currentUser.UserId,
                cancellationToken,
                reason: null,
                beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["armId"] = request.ArmId, ["termId"] = request.TermId, ["changes"] = beforeChanges })
                .ConfigureAwait(false);
        }

        var finalRatings = byPupilIndicator.Values
            .Select(rating => new DevelopmentRatingSnapshot(rating.PupilId, rating.IndicatorId, rating.RatingScalePointId, rating.Comment))
            .ToArray();

        var dto = DevelopmentRatingProjection.Build(
            armId, termId, resultSetRef, roster, sectionCheck.Value.ActiveDomainsOrdered, scales, finalRatings);

        return Result.Success(dto);
    }

    private static Dictionary<string, object?> Snapshot(Guid pupilId, Guid indicatorId, Guid? pointId, string? comment) => new(StringComparer.Ordinal)
    {
        ["pupilId"] = pupilId.ToString("D", CultureInfo.InvariantCulture),
        ["indicatorId"] = indicatorId.ToString("D", CultureInfo.InvariantCulture),
        ["pointId"] = pointId?.ToString("D", CultureInfo.InvariantCulture),
        ["comment"] = comment,
    };

    private static Dictionary<string, string[]> ValidateRows(
        IReadOnlyList<SaveDevelopmentRatingsRowInput> rows,
        HashSet<Guid> rosterPupilIds,
        Dictionary<Guid, (DevelopmentIndicator Indicator, DevelopmentDomain Domain)> indicatorsByDomain,
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

            foreach (var (key, cell) in row.Ratings ?? new Dictionary<string, DevelopmentRatingCellDto?>(StringComparer.Ordinal))
            {
                if (!Guid.TryParse(key, out var indicatorId))
                {
                    AddFailure($"Rows[{index}].Ratings[{key}]", "This is not a valid indicator id.");
                    continue;
                }

                if (!indicatorsByDomain.TryGetValue(indicatorId, out var entry))
                {
                    AddFailure($"Rows[{index}].Ratings[{key}]", "This is not a known indicator.");
                    continue;
                }

                var (indicator, domain) = entry;
                var ratable = indicator.Status == DevelopmentIndicatorStatus.Active && domain.Status == DevelopmentDomainStatus.Active;
                if (!ratable)
                {
                    AddFailure($"Rows[{index}].Ratings[{key}]", "This indicator is archived and cannot be rated.");
                    continue;
                }

                if (cell is null)
                {
                    // A whole-null cell clears the rating — nothing further to validate.
                    continue;
                }

                if (cell.PointId is null)
                {
                    // Q3-A: clearing the point clears the comment — valid only when no comment rides along.
                    if (!string.IsNullOrWhiteSpace(cell.Comment))
                    {
                        AddFailure($"Rows[{index}].Ratings[{key}]", "A comment requires a rating. Choose a point, or clear the comment too.");
                    }

                    continue;
                }

                if (!Guid.TryParse(cell.PointId, out var pointId))
                {
                    AddFailure($"Rows[{index}].Ratings[{key}]", "This is not a valid point id.");
                    continue;
                }

                var scale = scalesById[domain.RatingScaleId];
                if (!scale.Points.Any(point => point.Id == pointId))
                {
                    AddFailure($"Rows[{index}].Ratings[{key}]", $"This point is not on {scale.Name}.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(cell.Comment))
                {
                    continue;
                }

                var trimmedComment = cell.Comment.Trim();

                if (trimmedComment.Length > DevelopmentRating.CommentMaxLength)
                {
                    AddFailure(
                        $"Rows[{index}].Ratings[{key}]",
                        $"Comment must be {DevelopmentRating.CommentMaxLength} characters or fewer.");
                    continue;
                }

                if (!domain.AllowsIndicatorComment)
                {
                    AddFailure($"Rows[{index}].Ratings[{key}]", $"{domain.Name} does not allow a comment on its indicators.");
                }
            }
        }

        return failures.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal);
    }
}
