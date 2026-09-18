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
/// THE SNAPSHOT CARRIES EVERY GROUP, NOT ONLY THE ONE THAT CHANGED (spec 6.2.9) — this handler reads
/// the CURRENT grading, assessment and result-rules state (unchanged by this save) purely to hand
/// them to <see cref="SettingsSnapshotBuilder.Build"/>, matching every other settings handler.
/// </para>
/// </remarks>
internal sealed class UpdateRatingScalesCommandHandler(
    ISchoolProfileRepository schoolProfileRepository,
    IRatingScaleRepository ratingScaleRepository,
    IGradingBandRepository gradingBandRepository,
    IAssessmentComponentRepository assessmentComponentRepository,
    IResultRulesRepository resultRulesRepository,
    IConfigVersionRepository configVersionRepository,
    IAcademicSessionRepository academicSessionRepository,
    IPublishedResultsGate publishedResultsGate,
    IRatingScaleUsageGate ratingScaleUsageGate,
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

        var existingScales = await ratingScaleRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
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

        var currentBands = await gradingBandRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var currentComponents = await assessmentComponentRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var resultRules = await resultRulesRepository.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);

        var configVersion = ConfigVersion.Create(
            Guid.CreateVersion7(),
            SettingsSnapshotBuilder.Build(profile, currentBands, currentComponents, resultRules, scales),
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

        return Result.Success(SettingsMapper.ToRatingScalesDto(scales, profile.RatingScalesVersionNumber));
    }
}
