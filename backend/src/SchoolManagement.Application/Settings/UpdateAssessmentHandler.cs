using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="UpdateAssessmentCommand"/>.</summary>
/// <remarks>
/// <para>
/// ORDER OF CHECKS: stale version, then the 6.2.9 reason gate, then id resolution (every non-null
/// submitted id must reference a row that actually exists), then the six structural rules
/// (<see cref="AssessmentStructureRules"/>), then the session lock. Structural validity is checked
/// BEFORE the lock so a garbled submission (for example, totalling 90) fails with the specific
/// structural message rather than a generic lock rejection when it happens to also add a component.
/// </para>
/// <para>
/// THE SESSION LOCK (spec 6.2.6) is evaluated only when
/// <see cref="ISubjectScoreSessionLockLookup.AnyScoreExistsInSessionAsync"/> reports a mark entered
/// anywhere in the CURRENTLY ACTIVE session — see that interface's remarks for why it is honestly
/// <see langword="false"/> today, making this branch real, tested code with no production trigger yet.
/// Locked, this handler rejects an add, a removal, or a maximum/examination-flag change on an
/// existing id; rename and reorder stay allowed, matching 6.2.6 exactly. <see cref="AssessmentComponent.IsExamination"/>
/// is locked alongside <see cref="AssessmentComponent.MaxMark"/> — spec 6.2.6's own sentence names
/// only "a maximum" — because flipping which existing component counts as the examination would
/// silently move already-entered marks between the CA total and the exam total, the identical
/// "arithmetic nonsense" 6.2.6 invokes to justify locking maximums at all. Authored extension; see
/// <c>backend/docs/ASSUMPTIONS.md</c>.
/// </para>
/// <para>
/// APPLY STEP: matched ids are edited in place (<see cref="AssessmentComponent.Rename"/> then
/// <see cref="AssessmentComponent.ChangeMaximumAndKind"/> — harmless no-ops under a lock that already
/// proved nothing changed); unmatched submissions become new rows; existing rows absent from the
/// submission are removed. <c>displayOrder</c> is assigned from array position among the
/// NON-EXAMINATION components, with the examination component always placed last regardless of where
/// it sat in the submitted array (spec 6.2.6).
/// </para>
/// </remarks>
internal sealed class UpdateAssessmentCommandHandler(
    ISchoolProfileRepository schoolProfileRepository,
    IAssessmentComponentRepository assessmentComponentRepository,
    IGradingBandRepository gradingBandRepository,
    IConfigVersionRepository configVersionRepository,
    IAcademicSessionRepository academicSessionRepository,
    IPublishedResultsGate publishedResultsGate,
    ISubjectScoreSessionLockLookup subjectScoreSessionLockLookup,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateAssessmentCommand, Result<SettingsAssessmentGroupDto>>
{
    /// <summary>Stable error code for a stale <see cref="SchoolProfile.AssessmentVersionNumber"/> save.</summary>
    public const string StaleVersionErrorCode = "settings.assessment.stale_version";

    /// <summary>Stable error code for spec 6.2.6's session lock (6.2.12: "Returns 409 when locked for the session").</summary>
    public const string StructureLockedErrorCode = "settings.assessment.structure_locked";

    /// <summary>Stable error code for a submitted id that does not match any existing component.</summary>
    public const string UnknownComponentIdErrorCode = "settings.assessment.unknown_component_id";

    /// <summary>Stable error code for the same id submitted more than once.</summary>
    public const string DuplicateComponentIdErrorCode = "settings.assessment.duplicate_component_id";

    private const string SchoolProfileEntityType = "school_profile";

    /// <inheritdoc />
    public async Task<Result<SettingsAssessmentGroupDto>> HandleAsync(
        UpdateAssessmentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await schoolProfileRepository
            .GetTrackedSingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var profileIdText = profile.Id.ToString("D", CultureInfo.InvariantCulture);

        if (profile.AssessmentVersionNumber != request.ExpectedVersion)
        {
            await auditSink.RecordRejectionAsync(
                "settings.assessment.save_rejected_stale_version",
                SchoolProfileEntityType,
                profileIdText,
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["expectedVersion"] = request.ExpectedVersion,
                    ["currentVersion"] = profile.AssessmentVersionNumber,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<SettingsAssessmentGroupDto>(Error.Conflict(
                StaleVersionErrorCode,
                "The assessment structure was changed by another administrator while you were editing. " +
                "Reload and make your change again."));
        }

        var reasonCheck = await PublishedResultsReasonGate
            .RequireReasonIfPublishedAsync(request.Reason, academicSessionRepository, publishedResultsGate, cancellationToken)
            .ConfigureAwait(false);
        if (reasonCheck.IsFailure)
        {
            return Result.Failure<SettingsAssessmentGroupDto>(reasonCheck.Error);
        }

        var existing = await assessmentComponentRepository.ListTrackedAsync(cancellationToken).ConfigureAwait(false);
        var existingById = existing.ToDictionary(component => component.Id);

        var parsed = new List<(AssessmentComponentSaveRequest Request, Guid? Id)>(request.Components.Count);
        var seenIds = new HashSet<Guid>();
        foreach (var item in request.Components)
        {
            if (item.Id is null)
            {
                parsed.Add((item, null));
                continue;
            }

            // UpdateAssessmentCommandValidator already confirmed this parses as a Guid.
            var parsedId = Guid.Parse(item.Id, CultureInfo.InvariantCulture);

            if (!existingById.ContainsKey(parsedId))
            {
                return Result.Failure<SettingsAssessmentGroupDto>(Error.Validation(
                    UnknownComponentIdErrorCode,
                    Invariant($"Component {parsedId} does not exist.")));
            }

            if (!seenIds.Add(parsedId))
            {
                return Result.Failure<SettingsAssessmentGroupDto>(Error.Validation(
                    DuplicateComponentIdErrorCode,
                    "The same component id was submitted more than once."));
            }

            parsed.Add((item, parsedId));
        }

        var structuralInputs = parsed
            .Select(entry => new AssessmentComponentInput(entry.Id, entry.Request.Name, entry.Request.ShortLabel, entry.Request.MaxMark, entry.Request.IsExamination))
            .ToList();

        var validation = AssessmentStructureRules.ValidateWholeStructure(structuralInputs);
        if (validation.IsFailure)
        {
            return Result.Failure<SettingsAssessmentGroupDto>(validation.Error);
        }

        var activeSession = await academicSessionRepository.FindActiveAsync(cancellationToken).ConfigureAwait(false);
        if (activeSession is not null)
        {
            var locked = await subjectScoreSessionLockLookup
                .AnyScoreExistsInSessionAsync(activeSession.Id, cancellationToken)
                .ConfigureAwait(false);

            if (locked)
            {
                var submittedIds = parsed.Where(entry => entry.Id is not null).Select(entry => entry.Id!.Value).ToHashSet();
                var hasAddition = parsed.Any(entry => entry.Id is null);
                var hasRemoval = existingById.Keys.Except(submittedIds).Any();
                var hasMaximumOrKindChange = parsed.Any(entry =>
                    entry.Id is { } id &&
                    (existingById[id].MaxMark != entry.Request.MaxMark || existingById[id].IsExamination != entry.Request.IsExamination));

                if (hasAddition || hasRemoval || hasMaximumOrKindChange)
                {
                    return Result.Failure<SettingsAssessmentGroupDto>(Error.Conflict(
                        StructureLockedErrorCode,
                        Invariant($"Marks have already been entered in {activeSession.Name}. The assessment structure is locked until the session closes. You can still rename or reorder components.")));
                }
            }
        }

        // Non-examination components keep their submitted relative order; the examination (exactly
        // one, guaranteed by rule 2 above) is always placed last, regardless of submitted position.
        var ordered = parsed.Where(entry => !entry.Request.IsExamination)
            .Concat(parsed.Where(entry => entry.Request.IsExamination))
            .ToList();

        var savedComponents = new List<AssessmentComponent>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            var (componentRequest, id) = ordered[index];
            var displayOrder = index + 1;

            if (id is { } existingId)
            {
                var entity = existingById[existingId];
                entity.Rename(componentRequest.Name, componentRequest.ShortLabel, displayOrder);
                entity.ChangeMaximumAndKind(componentRequest.MaxMark, componentRequest.IsExamination, displayOrder);
                savedComponents.Add(entity);
            }
            else
            {
                var entity = AssessmentComponent.Create(
                    Guid.CreateVersion7(),
                    componentRequest.Name,
                    componentRequest.ShortLabel,
                    componentRequest.MaxMark,
                    componentRequest.IsExamination,
                    displayOrder);

                await assessmentComponentRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
                savedComponents.Add(entity);
            }
        }

        var keptIds = ordered.Where(entry => entry.Id is not null).Select(entry => entry.Id!.Value).ToHashSet();
        foreach (var removed in existing.Where(component => !keptIds.Contains(component.Id)))
        {
            await assessmentComponentRepository.RemoveAsync(removed, cancellationToken).ConfigureAwait(false);
        }

        profile.IncrementAssessmentVersion();

        var currentBands = await gradingBandRepository.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);

        var configVersion = ConfigVersion.Create(
            Guid.CreateVersion7(),
            SettingsSnapshotBuilder.Build(profile, currentBands, savedComponents),
            ConfigVersionGroup.Assessment,
            currentUser.UserId,
            reason: reasonCheck.Value,
            now);

        await configVersionRepository.AddAsync(configVersion, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            "settings.assessment.updated",
            SchoolProfileEntityType,
            profileIdText,
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["componentCount"] = savedComponents.Count },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SettingsMapper.ToAssessmentDto(savedComponents, profile.AssessmentVersionNumber));
    }

    private static string Invariant(FormattableString formattable) =>
        formattable.ToString(CultureInfo.InvariantCulture);
}
