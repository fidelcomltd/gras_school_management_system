using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="UpdateGradingCommand"/>.</summary>
/// <remarks>
/// <para>
/// OPTIMISTIC CONCURRENCY, same pattern as <c>UpdateAbbreviationCommandHandler</c>:
/// <see cref="UpdateGradingCommand.ExpectedVersion"/> is compared against
/// <see cref="SchoolProfile.GradingVersionNumber"/> BEFORE anything is validated or written.
/// </para>
/// <para>
/// THE 6.2.9 REASON GATE: <see cref="IPublishedResultsGate.CountPublishedInSessionAsync"/> is checked
/// against the currently active session (if any). When it reports at least one published result set,
/// <see cref="UpdateGradingCommand.Reason"/> must be present and at least ten characters (already
/// length-checked by <c>UpdateGradingCommandValidator</c> when non-null; presence is checked here
/// because it is conditional on live state a command validator cannot see). The gate is a documented
/// seam that honestly reports zero today — see its own remarks — so this branch is real, tested code
/// that cannot yet be exercised in production.
/// </para>
/// <para>
/// VALIDATE-THEN-WRITE ORDER: <see cref="GradingScaleRules.ValidateWholeScale"/> runs BEFORE the
/// replace, so an invalid submission never reaches <see cref="IGradingBandRepository.ReplaceAllAsync"/>
/// — 6.2.5's "a partially valid scale is never saved."
/// </para>
/// <para>
/// THE SNAPSHOT CARRIES EVERY GROUP, NOT ONLY THE ONE THAT CHANGED (spec 6.2.9) — this handler reads
/// the CURRENT assessment structure (unchanged by this save) purely to hand it to
/// <see cref="SettingsSnapshotBuilder.Build"/>, matching what <c>UpdateAssessmentCommandHandler</c>
/// does in the other direction.
/// </para>
/// </remarks>
internal sealed class UpdateGradingCommandHandler(
    ISchoolProfileRepository schoolProfileRepository,
    IGradingBandRepository gradingBandRepository,
    IAssessmentComponentRepository assessmentComponentRepository,
    IConfigVersionRepository configVersionRepository,
    IAcademicSessionRepository academicSessionRepository,
    IPublishedResultsGate publishedResultsGate,
    IResultRulesRepository resultRulesRepository,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateGradingCommand, Result<SettingsGradingGroupDto>>
{
    /// <summary>Stable error code for a stale <see cref="SchoolProfile.GradingVersionNumber"/> save.</summary>
    public const string StaleVersionErrorCode = "settings.grading.stale_version";

    private const string SchoolProfileEntityType = "school_profile";

    /// <inheritdoc />
    public async Task<Result<SettingsGradingGroupDto>> HandleAsync(
        UpdateGradingCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await schoolProfileRepository
            .GetTrackedSingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var profileIdText = profile.Id.ToString("D", CultureInfo.InvariantCulture);

        if (profile.GradingVersionNumber != request.ExpectedVersion)
        {
            await auditSink.RecordRejectionAsync(
                "settings.grading.save_rejected_stale_version",
                SchoolProfileEntityType,
                profileIdText,
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["expectedVersion"] = request.ExpectedVersion,
                    ["currentVersion"] = profile.GradingVersionNumber,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<SettingsGradingGroupDto>(Error.Conflict(
                StaleVersionErrorCode,
                "The grading scale was changed by another administrator while you were editing. " +
                "Reload and make your change again."));
        }

        var reasonCheck = await PublishedResultsReasonGate
            .RequireReasonIfPublishedAsync(request.Reason, academicSessionRepository, publishedResultsGate, cancellationToken)
            .ConfigureAwait(false);
        if (reasonCheck.IsFailure)
        {
            return Result.Failure<SettingsGradingGroupDto>(reasonCheck.Error);
        }

        var validation = GradingScaleRules.ValidateWholeScale(request.Bands);
        if (validation.IsFailure)
        {
            return Result.Failure<SettingsGradingGroupDto>(validation.Error);
        }

        var bands = request.Bands
            .Select((input, index) => GradingBand.Create(
                Guid.CreateVersion7(),
                input.LowerBound,
                input.UpperBound,
                input.GradeLetter,
                input.Remark,
                displayOrder: index + 1))
            .ToList();

        await gradingBandRepository.ReplaceAllAsync(bands, cancellationToken).ConfigureAwait(false);

        profile.IncrementGradingVersion();

        var currentComponents = await assessmentComponentRepository
            .ListReadOnlyOrderedAsync(cancellationToken)
            .ConfigureAwait(false);
        var resultRules = await resultRulesRepository.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);

        var configVersion = ConfigVersion.Create(
            Guid.CreateVersion7(),
            SettingsSnapshotBuilder.Build(profile, bands, currentComponents, resultRules),
            ConfigVersionGroup.Grading,
            currentUser.UserId,
            reason: reasonCheck.Value,
            now);

        await configVersionRepository.AddAsync(configVersion, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            "settings.grading.updated",
            SchoolProfileEntityType,
            profileIdText,
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["bandCount"] = bands.Count },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SettingsMapper.ToGradingDto(bands, profile.GradingVersionNumber));
    }

}
