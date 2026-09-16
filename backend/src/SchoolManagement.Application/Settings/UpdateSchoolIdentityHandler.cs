using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="UpdateSchoolIdentityCommand"/>.</summary>
/// <remarks>
/// <para>
/// OPTIMISTIC CONCURRENCY, PER THE APPROVED DELTA'S DESIGN (not the generic EF shadow-property
/// token): <see cref="UpdateSchoolIdentityCommand.ExpectedVersion"/> is compared against
/// <see cref="SchoolProfile.IdentityVersionNumber"/> BEFORE anything is mutated. A mismatch returns
/// <c>409</c> and records only an audit-seam call — the loser never reaches
/// <see cref="SchoolProfile.UpdateIdentity"/> and so never causes a <see cref="ConfigVersion"/> row to
/// be staged, matching spec 6.2.11's "both attempts appear in the audit log" (not "both attempts get
/// a version row"). The unit-of-work behaviour then rolls back the (empty) transaction for the
/// failure path exactly as it would for any other returned <see cref="Result"/> failure.
/// </para>
/// <para>
/// The <c>409</c> message below is AUTHORED COPY, not spec copy — 6.2.11's sentence names the grading
/// scale, a group this card never touches. See <c>backend/docs/ASSUMPTIONS.md</c> §2.15 (approved
/// delta amendment 3).
/// </para>
/// </remarks>
internal sealed class UpdateSchoolIdentityCommandHandler(
    ISchoolProfileRepository schoolProfileRepository,
    IConfigVersionRepository configVersionRepository,
    IGradingBandRepository gradingBandRepository,
    IAssessmentComponentRepository assessmentComponentRepository,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateSchoolIdentityCommand, Result<SettingsIdentityGroupDto>>
{
    /// <summary>Stable error code for a stale <see cref="SchoolProfile.IdentityVersionNumber"/> save.</summary>
    public const string StaleVersionErrorCode = "settings.identity.stale_version";

    private const string SchoolProfileEntityType = "school_profile";

    /// <inheritdoc />
    public async Task<Result<SettingsIdentityGroupDto>> HandleAsync(
        UpdateSchoolIdentityCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await schoolProfileRepository
            .GetTrackedSingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var profileIdText = profile.Id.ToString("D", CultureInfo.InvariantCulture);

        if (profile.IdentityVersionNumber != request.ExpectedVersion)
        {
            // RecordRejectionAsync (not RecordAsync): this row must survive the ambient
            // transaction's rollback below, which a same-transaction write would not (TASK-0048) —
            // otherwise this rejection, like every other one, would be silently lost.
            await auditSink.RecordRejectionAsync(
                "settings.identity.save_rejected_stale_version",
                SchoolProfileEntityType,
                profileIdText,
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["expectedVersion"] = request.ExpectedVersion,
                    ["currentVersion"] = profile.IdentityVersionNumber,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<SettingsIdentityGroupDto>(Error.Conflict(
                StaleVersionErrorCode,
                "The school identity was changed by another administrator while you were editing. " +
                "Reload and make your change again."));
        }

        profile.UpdateIdentity(
            request.SchoolName,
            request.ShortName,
            request.Address,
            request.Phone,
            request.Email,
            request.Motto,
            request.HeadTeacherName);

        // TASK-0069: the snapshot carries every group, not only the one this save changed (spec
        // 6.2.9) — read the grading/assessment groups' CURRENT state purely to hand them through.
        var bands = await gradingBandRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var components = await assessmentComponentRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);

        var configVersion = ConfigVersion.Create(
            Guid.CreateVersion7(),
            SettingsSnapshotBuilder.Build(profile, bands, components),
            ConfigVersionGroup.Identity,
            currentUser.UserId,
            reason: null,
            now);

        await configVersionRepository.AddAsync(configVersion, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            "settings.identity.updated",
            SchoolProfileEntityType,
            profileIdText,
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SettingsMapper.ToIdentityDto(profile));
    }
}
