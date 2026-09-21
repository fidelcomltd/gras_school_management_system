using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="UpdateRatingScalesCommand"/>.</summary>
/// <remarks>
/// <para>
/// OPTIMISTIC CONCURRENCY, same pattern as <c>UpdateGradingCommandHandler</c>:
/// <see cref="UpdateRatingScalesCommand.ExpectedVersion"/> is compared against
/// <see cref="SchoolProfile.RatingScalesVersionNumber"/> BEFORE anything is validated or written.
/// </para>
/// <para>
/// IDS ARE STABLE ACROSS A SAVE (TASK-0072 stage 1 review fix — this handler originally matched a
/// removal by NAME and minted a fresh id for every scale/point on every save, which is fine for
/// <c>GradingBand</c>, nothing references, but wrong here: stage 2/3 rating blocks reference a scale
/// BY ID, and a rescale save must never silently change it out from under them). A submitted scale or
/// point carrying an <c>id</c> is updated in place, id preserved; one with no <c>id</c> is new. An
/// EXISTING scale whose id is absent from the whole submitted set is the one checked against
/// <see cref="IRatingScaleUsageGate"/> before anything is written — <c>409
/// settings.ratingscales.in_use</c> when it is still referenced. A submitted id that matches no
/// current row is rejected <c>422 settings.ratingscales.unknown_scale_id</c> /
/// <c>settings.ratingscales.unknown_point_id</c>, naming the offending position.
/// </para>
/// <para>
/// THE SNAPSHOT CARRIES EVERY GROUP, NOT ONLY THE ONE THAT CHANGED (spec 6.2.9) — TASK-0072 stage 3a:
/// this handler asks <see cref="ISettingsSnapshotSource"/> for every OTHER group's current state in
/// one call and overrides only the rating-scales group with the just-saved, pre-commit
/// <c>scales</c> before handing the result to <see cref="SettingsSnapshotBuilder.Build"/>.
/// </para>
/// </remarks>
internal sealed class UpdateRatingScalesCommandHandler(
    ISchoolProfileRepository schoolProfileRepository,
    IRatingScaleRepository ratingScaleRepository,
    IConfigVersionRepository configVersionRepository,
    IAcademicSessionRepository academicSessionRepository,
    IPublishedResultsGate publishedResultsGate,
    IRatingScaleUsageGate ratingScaleUsageGate,
    IResultSetRepository resultSetRepository,
    ISettingsSnapshotSource settingsSnapshotSource,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateRatingScalesCommand, Result<SettingsRatingScaleGroupDto>>
{
    /// <summary>Stable error code for a stale <see cref="SchoolProfile.RatingScalesVersionNumber"/> save.</summary>
    public const string StaleVersionErrorCode = "settings.ratingscales.stale_version";

    /// <summary>Stable error code for removing a scale a rating block still references.</summary>
    public const string InUseErrorCode = "settings.ratingscales.in_use";

    /// <summary>Stable error code for a submitted scale id that matches no current scale.</summary>
    public const string UnknownScaleIdErrorCode = "settings.ratingscales.unknown_scale_id";

    /// <summary>Stable error code for a submitted point id that matches no current point of its owning scale.</summary>
    public const string UnknownPointIdErrorCode = "settings.ratingscales.unknown_point_id";

    /// <summary>Stable error code for removing a point any rating still FKs (TASK-0083 stage 3, R2's point rule).</summary>
    public const string PointRatedErrorCode = "settings.ratingscales.point_rated";

    private const string SchoolProfileEntityType = "school_profile";

    /// <inheritdoc />
    public async Task<Result<SettingsRatingScaleGroupDto>> HandleAsync(
        UpdateRatingScalesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await schoolProfileRepository
            .GetTrackedSingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var profileIdText = profile.Id.ToString("D", CultureInfo.InvariantCulture);

        if (profile.RatingScalesVersionNumber != request.ExpectedVersion)
        {
            await auditSink.RecordRejectionAsync(
                "settings.ratingscales.save_rejected_stale_version",
                SchoolProfileEntityType,
                profileIdText,
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["expectedVersion"] = request.ExpectedVersion,
                    ["currentVersion"] = profile.RatingScalesVersionNumber,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<SettingsRatingScaleGroupDto>(Error.Conflict(
                StaleVersionErrorCode,
                "The rating scales were changed by another administrator while you were editing. " +
                "Reload and make your change again."));
        }

        var existingScales = await ratingScaleRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);

        // R2 (point_rated), stage 3: BEFORE the reason gate, structural validation or the existing
        // in-use gate — removing a point that any rating still FKs is refused, Published sets
        // included, since a delete would otherwise surface downstream as a 500, not a clean 409.
        var pointRatedResult = await RefuseIfAnyRemovedPointIsRatedAsync(request.Scales, existingScales, cancellationToken)
            .ConfigureAwait(false);
        if (pointRatedResult is not null)
        {
            return pointRatedResult;
        }

        var reasonCheck = await PublishedResultsReasonGate
            .RequireReasonIfPublishedAsync(request.Reason, academicSessionRepository, publishedResultsGate, cancellationToken)
            .ConfigureAwait(false);
        if (reasonCheck.IsFailure)
        {
            return Result.Failure<SettingsRatingScaleGroupDto>(reasonCheck.Error);
        }

        var validation = RatingScaleRules.ValidateWholeSet(request.Scales);
        if (validation.IsFailure)
        {
            return Result.Failure<SettingsRatingScaleGroupDto>(validation.Error);
        }

        var existingScalesById = existingScales.ToDictionary(scale => scale.Id);
        var submittedScaleIds = request.Scales
            .Where(scale => scale.Id is not null)
            .Select(scale => scale.Id!.Value)
            .ToHashSet();

        // A submitted scale id that matches no current scale — 422, names the offending position.
        for (var scaleIndex = 0; scaleIndex < request.Scales.Count; scaleIndex++)
        {
            var scaleInput = request.Scales[scaleIndex];
            if (scaleInput.Id is Guid submittedScaleId && !existingScalesById.ContainsKey(submittedScaleId))
            {
                return Result.Failure<SettingsRatingScaleGroupDto>(new RatingScaleValidationError(
                    UnknownScaleIdErrorCode,
                    $"Scale {scaleInput.Name} has an id that does not match any existing scale. Reload and try again.",
                    scaleIndex,
                    null));
            }

            var existingPointIds = scaleInput.Id is Guid ownerId && existingScalesById.TryGetValue(ownerId, out var ownerScale)
                ? ownerScale.Points.Select(point => point.Id).ToHashSet()
                : [];

            for (var pointIndex = 0; pointIndex < scaleInput.Points.Count; pointIndex++)
            {
                var pointInput = scaleInput.Points[pointIndex];
                if (pointInput.Id is Guid submittedPointId && !existingPointIds.Contains(submittedPointId))
                {
                    return Result.Failure<SettingsRatingScaleGroupDto>(new RatingScaleValidationError(
                        UnknownPointIdErrorCode,
                        $"Point {pointInput.PointCode} has an id that does not match any existing point of scale {scaleInput.Name}. Reload and try again.",
                        scaleIndex,
                        pointIndex));
                }
            }
        }

        // An EXISTING scale whose id is absent from the whole submitted set is being removed.
        foreach (var existingScale in existingScales)
        {
            if (submittedScaleIds.Contains(existingScale.Id))
            {
                continue;
            }

            var inUse = await ratingScaleUsageGate.IsInUseAsync(existingScale.Id, cancellationToken).ConfigureAwait(false);
            if (!inUse)
            {
                continue;
            }

            await auditSink.RecordRejectionAsync(
                "settings.ratingscales.save_rejected_in_use",
                "rating_scale",
                existingScale.Id.ToString("D", CultureInfo.InvariantCulture),
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["scaleName"] = existingScale.Name },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<SettingsRatingScaleGroupDto>(Error.Conflict(
                InUseErrorCode,
                $"The scale {existingScale.Name} is still in use by a rating block. Archive whatever uses it first, or keep the scale."));
        }

        var scales = request.Scales
            .Select(scaleInput =>
            {
                var scaleId = scaleInput.Id ?? Guid.CreateVersion7();
                var points = scaleInput.Points
                    .Select(pointInput => RatingScalePoint.Create(
                        pointInput.Id ?? Guid.CreateVersion7(),
                        scaleId,
                        pointInput.PointCode,
                        pointInput.PointLabel,
                        pointInput.PointOrder))
                    .ToList();

                return RatingScale.Create(scaleId, scaleInput.Name, points);
            })
            .ToList();

        await ratingScaleRepository.ReplaceAllAsync(scales, cancellationToken).ConfigureAwait(false);

        profile.IncrementRatingScalesVersion();

        var snapshotState = await settingsSnapshotSource.LoadAsync(cancellationToken).ConfigureAwait(false);

        var configVersion = ConfigVersion.Create(
            Guid.CreateVersion7(),
            SettingsSnapshotBuilder.Build(profile, snapshotState with { RatingScales = scales }),
            ConfigVersionGroup.RatingScales,
            currentUser.UserId,
            reason: reasonCheck.Value,
            now);

        await configVersionRepository.AddAsync(configVersion, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            "settings.ratingscales.updated",
            SchoolProfileEntityType,
            profileIdText,
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["scaleCount"] = scales.Count },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        // TASK-0088 AC A1: a rating-scales save flags every non-Published result set in the active session.
        var activeSessionForFlag = await academicSessionRepository.FindActiveAsync(cancellationToken).ConfigureAwait(false);
        if (activeSessionForFlag is not null)
        {
            var lockedResultSets = await resultSetRepository
                .LockNonPublishedInSessionAsync(activeSessionForFlag.Id, cancellationToken)
                .ConfigureAwait(false);

            await ResultSetRecomputeFlagger.FlagAsync(lockedResultSets, auditSink, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(SettingsMapper.ToRatingScalesDto(scales, profile.RatingScalesVersionNumber));
    }

    /// <summary>
    /// Walks every point about to disappear from persistence — because its whole owning scale is
    /// absent from <paramref name="submittedScales"/>, or because the scale is retained but the point
    /// itself is dropped from its submitted <c>points</c> array — and refuses on the FIRST one any
    /// rating still FKs, Published sets included (see <see cref="PointRatedErrorCode"/>'s remarks on
    /// <see cref="IRatingScaleUsageGate.IsPointRatedAsync"/>). An in-place update (id present, only
    /// label/code/order changed) never reaches this loop: the point's id is still present in
    /// <paramref name="submittedScales"/>, so it is never counted as "about to disappear". Returns
    /// <see langword="null"/> when nothing is refused, meaning the caller may proceed.
    /// </summary>
    private async Task<Result<SettingsRatingScaleGroupDto>?> RefuseIfAnyRemovedPointIsRatedAsync(
        IReadOnlyList<RatingScaleInput> submittedScales,
        IReadOnlyList<RatingScale> existingScales,
        CancellationToken cancellationToken)
    {
        var submittedScalesById = submittedScales
            .Where(scale => scale.Id is not null)
            .ToDictionary(scale => scale.Id!.Value, scale => scale);

        foreach (var existingScale in existingScales)
        {
            var submittedPointIds = submittedScalesById.TryGetValue(existingScale.Id, out var retainedScale)
                ? retainedScale.Points
                    .Where(point => point.Id is not null)
                    .Select(point => point.Id!.Value)
                    .ToHashSet()
                : [];

            foreach (var existingPoint in existingScale.Points)
            {
                if (submittedPointIds.Contains(existingPoint.Id))
                {
                    continue;
                }

                var rated = await ratingScaleUsageGate
                    .IsPointRatedAsync(existingPoint.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (!rated)
                {
                    continue;
                }

                await auditSink.RecordRejectionAsync(
                    "settings.ratingscales.save_rejected_point_rated",
                    "rating_scale_point",
                    existingPoint.Id.ToString("D", CultureInfo.InvariantCulture),
                    metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["pointLabel"] = existingPoint.PointLabel,
                        ["scaleName"] = existingScale.Name,
                    },
                    actorAdminId: currentUser.UserId,
                    cancellationToken).ConfigureAwait(false);

                return Result.Failure<SettingsRatingScaleGroupDto>(Error.Conflict(
                    PointRatedErrorCode,
                    $"Ratings have already been entered for {existingPoint.PointLabel} this term. Keep " +
                    "the point on the scale, or update its label and code instead of removing it."));
            }
        }

        return null;
    }
}
