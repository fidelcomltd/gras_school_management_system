using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="UpdateRegNumberCommand"/>.</summary>
/// <remarks>
/// <para>
/// OPTIMISTIC CONCURRENCY, same pattern as <c>UpdateSchoolIdentityCommandHandler</c>:
/// <see cref="UpdateRegNumberCommand.ExpectedVersion"/> is compared against
/// <see cref="SchoolProfile.RegNumberVersionNumber"/> BEFORE anything is mutated. A mismatch returns
/// <c>409</c> and records only an audit-seam rejection — the loser never reaches
/// <see cref="SchoolProfile.UpdateRegNumber"/> and so never stages a <see cref="ConfigVersion"/> row.
/// </para>
/// <para>
/// THE WIDTH-REDUCTION CHECK (spec 6.2.10, approved delta amendment 1's other half): reads
/// <see cref="IRegistrationCounterRepository.GetLastSerialAsync"/> through the SAME partition
/// <see cref="RegistrationCounterPartition"/> resolves for the group's CURRENTLY SAVED (pre-mutation)
/// <see cref="SchoolProfile.SerialReset"/> — the counter that is actually active today. A partition a
/// simultaneous <c>serialReset</c> change would switch TO has no issuance history yet under this
/// card's scope (no increment path exists), so it can never itself be the one with a serial too wide
/// to fit; checking the currently-active partition is the one that can genuinely reject.
/// </para>
/// <para>
/// The <c>409</c> stale-version message below is AUTHORED COPY, not spec copy — 6.2.11's sentence
/// names the grading scale, a group this card never touches. See <c>backend/docs/ASSUMPTIONS.md</c>
/// (approved delta amendment 3).
/// </para>
/// </remarks>
internal sealed class UpdateRegNumberCommandHandler(
    ISchoolProfileRepository schoolProfileRepository,
    IRegistrationCounterRepository registrationCounterRepository,
    IConfigVersionRepository configVersionRepository,
    ISettingsSnapshotSource settingsSnapshotSource,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateRegNumberCommand, Result<SettingsRegNumberGroupDto>>
{
    /// <summary>Stable error code for a stale <see cref="SchoolProfile.RegNumberVersionNumber"/> save.</summary>
    public const string StaleVersionErrorCode = "settings.regnumber.stale_version";

    /// <summary>Stable error code for spec 6.2.10's width-reduction rejection.</summary>
    public const string WidthTooSmallErrorCode = "settings.regnumber.width_too_small";

    private const string SchoolProfileEntityType = "school_profile";

    /// <inheritdoc />
    public async Task<Result<SettingsRegNumberGroupDto>> HandleAsync(
        UpdateRegNumberCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await schoolProfileRepository
            .GetTrackedSingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var profileIdText = profile.Id.ToString("D", CultureInfo.InvariantCulture);

        if (profile.RegNumberVersionNumber != request.ExpectedVersion)
        {
            await auditSink.RecordRejectionAsync(
                "settings.regnumber.save_rejected_stale_version",
                SchoolProfileEntityType,
                profileIdText,
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["expectedVersion"] = request.ExpectedVersion,
                    ["currentVersion"] = profile.RegNumberVersionNumber,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<SettingsRegNumberGroupDto>(Error.Conflict(
                StaleVersionErrorCode,
                "The registration number configuration was changed by another administrator while " +
                "you were editing. Reload and make your change again."));
        }

        var activePartitionKey = RegistrationCounterPartition.Resolve(profile.SerialReset, now.Year);
        var lastSerial = await registrationCounterRepository
            .GetLastSerialAsync(activePartitionKey, cancellationToken)
            .ConfigureAwait(false);

        if (lastSerial > 0)
        {
            var digitsNeeded = RegNumberFormat.DigitCount(lastSerial);
            if (digitsNeeded > request.SerialWidth)
            {
                return Result.Failure<SettingsRegNumberGroupDto>(Error.Conflict(
                    WidthTooSmallErrorCode,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Serial {lastSerial} will not fit in a width of {request.SerialWidth}. " +
                        $"Choose {digitsNeeded} or more.")));
            }
        }

        profile.UpdateRegNumber(request.Separator, request.SerialWidth, request.SerialReset);

        // TASK-0069/TASK-0077/TASK-0072 stage 3a: the snapshot carries every group, not only the one
        // this save changed (spec 6.2.9) — this group has no field of its own in SettingsSnapshotState
        // (it lives on SchoolProfile itself), so no override is needed.
        var snapshotState = await settingsSnapshotSource.LoadAsync(cancellationToken).ConfigureAwait(false);

        var configVersion = ConfigVersion.Create(
            Guid.CreateVersion7(),
            SettingsSnapshotBuilder.Build(profile, snapshotState),
            ConfigVersionGroup.RegistrationNumber,
            currentUser.UserId,
            reason: null,
            now);

        await configVersionRepository.AddAsync(configVersion, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            "settings.regnumber.updated",
            SchoolProfileEntityType,
            profileIdText,
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SettingsMapper.ToRegNumberDto(profile));
    }
}
