using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="UpdateAbbreviationCommand"/>.</summary>
/// <remarks>
/// <para>
/// OPTIMISTIC CONCURRENCY, same pattern as <c>UpdateSchoolIdentityCommandHandler</c>:
/// <see cref="UpdateAbbreviationCommand.ExpectedVersion"/> is compared against
/// <see cref="SchoolProfile.AbbreviationVersionNumber"/> BEFORE anything is mutated. A mismatch
/// returns <c>409</c> and records only an audit-seam rejection — the loser never reaches
/// <see cref="SchoolProfile.UpdateAbbreviation"/> and so never stages a <see cref="ConfigVersion"/>
/// row.
/// </para>
/// <para>
/// Rewrites NO issued number and does not touch the registration counter at all (this card owns no
/// increment path) — the counter is keyed on admission year alone, never the abbreviation (spec
/// 6.2.4), so this handler has nothing to do to it either way.
/// </para>
/// <para>
/// The <c>409</c> message below is AUTHORED COPY, not spec copy — 6.2.11's sentence names the grading
/// scale, a group this card never touches. See <c>backend/docs/ASSUMPTIONS.md</c> (approved delta
/// amendment 3).
/// </para>
/// </remarks>
internal sealed class UpdateAbbreviationCommandHandler(
    ISchoolProfileRepository schoolProfileRepository,
    IConfigVersionRepository configVersionRepository,
    ISettingsSnapshotSource settingsSnapshotSource,
    IPupilRepository pupils,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateAbbreviationCommand, Result<SettingsAbbreviationGroupDto>>
{
    /// <summary>Stable error code for a stale <see cref="SchoolProfile.AbbreviationVersionNumber"/> save.</summary>
    public const string StaleVersionErrorCode = "settings.abbreviation.stale_version";

    private const string SchoolProfileEntityType = "school_profile";

    /// <inheritdoc />
    public async Task<Result<SettingsAbbreviationGroupDto>> HandleAsync(
        UpdateAbbreviationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await schoolProfileRepository
            .GetTrackedSingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var profileIdText = profile.Id.ToString("D", CultureInfo.InvariantCulture);

        if (profile.AbbreviationVersionNumber != request.ExpectedVersion)
        {
            await auditSink.RecordRejectionAsync(
                "settings.abbreviation.save_rejected_stale_version",
                SchoolProfileEntityType,
                profileIdText,
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["expectedVersion"] = request.ExpectedVersion,
                    ["currentVersion"] = profile.AbbreviationVersionNumber,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<SettingsAbbreviationGroupDto>(Error.Conflict(
                StaleVersionErrorCode,
                "The abbreviation was changed by another administrator while you were editing. " +
                "Reload and make your change again."));
        }

        var previousAbbreviation = profile.Abbreviation;
        profile.UpdateAbbreviation(request.Abbreviation);

        // TASK-0069/TASK-0077/TASK-0072 stage 3a: the snapshot carries every group, not only the one
        // this save changed (spec 6.2.9) — this group has no field of its own in SettingsSnapshotState
        // (it lives on SchoolProfile itself), so no override is needed.
        var snapshotState = await settingsSnapshotSource.LoadAsync(cancellationToken).ConfigureAwait(false);

        var configVersion = ConfigVersion.Create(
            Guid.CreateVersion7(),
            SettingsSnapshotBuilder.Build(profile, snapshotState),
            ConfigVersionGroup.Abbreviation,
            currentUser.UserId,
            request.Reason.Trim(),
            now);

        await configVersionRepository.AddAsync(configVersion, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            "settings.abbreviation.updated",
            SchoolProfileEntityType,
            profileIdText,
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["previousAbbreviation"] = previousAbbreviation,
                ["newAbbreviation"] = profile.Abbreviation,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        // TASK-0051: a live count against the NEW abbreviation, never null — see GetSettingsQueryHandler.
        var issuedCount = await pupils
            .CountByRegistrationNumberPrefixAsync(profile.Abbreviation, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(SettingsMapper.ToAbbreviationDto(profile, issuedCount));
    }
}
