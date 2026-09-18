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

/// <summary>Handles <see cref="ResetGradingCommand"/>.</summary>
/// <remarks>
/// Builds fresh <see cref="GradingBand"/> rows straight from <see cref="GradingScaleSeed.SeededBands"/>
/// and replaces the whole table through <see cref="IGradingBandRepository.ReplaceAllAsync"/> — the
/// same write path <c>UpdateGradingCommandHandler</c> uses, because a reset is, mechanically, an
/// ordinary save whose input happens to be the seed rather than client-submitted rows (spec 6.2.11:
/// "treated as an ordinary edit under 6.2.9"). The seed is not re-validated against
/// <see cref="GradingScaleRules"/> — it is a compile-time constant already known to satisfy all ten
/// rules — the same trust <c>SchoolProfileConfiguration.HasData</c> places in its own seed values.
/// </remarks>
internal sealed class ResetGradingCommandHandler(
    ISchoolProfileRepository schoolProfileRepository,
    IGradingBandRepository gradingBandRepository,
    IConfigVersionRepository configVersionRepository,
    IAcademicSessionRepository academicSessionRepository,
    IPublishedResultsGate publishedResultsGate,
    ISettingsSnapshotSource settingsSnapshotSource,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<ResetGradingCommand, Result<SettingsGradingGroupDto>>
{
    /// <summary>Stable error code for a stale <see cref="SchoolProfile.GradingVersionNumber"/> save.</summary>
    public const string StaleVersionErrorCode = "settings.grading.stale_version";

    private const string SchoolProfileEntityType = "school_profile";

    /// <inheritdoc />
    public async Task<Result<SettingsGradingGroupDto>> HandleAsync(
        ResetGradingCommand request,
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

        var bands = GradingScaleSeed.SeededBands
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

        var snapshotState = await settingsSnapshotSource.LoadAsync(cancellationToken).ConfigureAwait(false);

        var configVersion = ConfigVersion.Create(
            Guid.CreateVersion7(),
            SettingsSnapshotBuilder.Build(profile, snapshotState with { GradingBands = bands }),
            ConfigVersionGroup.Grading,
            currentUser.UserId,
            reason: reasonCheck.Value,
            now);

        await configVersionRepository.AddAsync(configVersion, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            "settings.grading.reset_to_defaults",
            SchoolProfileEntityType,
            profileIdText,
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["bandCount"] = bands.Count },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SettingsMapper.ToGradingDto(bands, profile.GradingVersionNumber));
    }
}
