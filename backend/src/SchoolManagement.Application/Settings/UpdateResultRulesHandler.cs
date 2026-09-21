using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="UpdateResultRulesCommand"/>.</summary>
/// <remarks>
/// <para>
/// ORDER OF CHECKS, following <c>UpdateAssessmentCommandHandler</c>'s documented order exactly: stale
/// version, then the 6.2.9 reason gate, then core-subject id resolution (every non-empty submitted id
/// must reference an existing ACTIVE subject), then the two 6.2.10 hard locks, then apply. Orchestrator
/// ruling 2026-09-18 (TASK-0077 review) amended the originally-approved delta to add
/// <see cref="UpdateResultRulesCommand.ExpectedVersion"/> — <see cref="SchoolProfile.ResultRulesVersionNumber"/>
/// is the group's pointer, matching <see cref="SchoolProfile.AssessmentVersionNumber"/>'s own precedent
/// (the rules/components live in their own table either way; the counter lives on the profile
/// regardless, per <see cref="SchoolProfile.GradingVersionNumber"/>'s remarks).
/// </para>
/// <para>
/// TWO INDEPENDENT LOCKS (spec 6.2.8/6.2.10), never conflated: (a) <see cref="ResultRules.PrimaryPositionScope"/>
/// and <see cref="ResultRules.TieBreakRule"/> are refused once <see cref="IPublishedResultsGate.CountPublishedInSessionAsync"/>
/// reports anything Published in the active session; (b) <see cref="ResultRules.AnnualMethod"/> and the
/// three weights are refused once <see cref="IPublishedResultsGate.AnyThirdTermPublishedInSessionAsync"/>
/// reports Third Term published for any arm. Both share the SAME error code, <see cref="LockedErrorCode"/>
/// (orchestrator ruling 2026-09-18: renamed from the card's original literal <c>result_rules_locked</c>
/// to this dotted form, matching <c>settings.assessment.structure_locked</c>'s convention and the
/// <c>settings.resultrules.update</c> privilege token). CHANGING A LOCKED FIELD TO ITS CURRENT VALUE IS
/// NOT A CHANGE: both checks compare the submitted value against the CURRENT row first and only query
/// the gate when something would actually change, so a same-value resubmission never trips a lock.
/// </para>
/// <para>
/// THE SNAPSHOT CARRIES EVERY GROUP (spec 6.2.9) — reads the CURRENT grading/assessment state purely
/// to hand it to <see cref="SettingsSnapshotBuilder.Build"/>, matching every other settings handler.
/// </para>
/// </remarks>
internal sealed class UpdateResultRulesCommandHandler(
    IResultRulesRepository resultRulesRepository,
    ISchoolProfileRepository schoolProfileRepository,
    IConfigVersionRepository configVersionRepository,
    IAcademicSessionRepository academicSessionRepository,
    IPublishedResultsGate publishedResultsGate,
    ISubjectRepository subjectRepository,
    IResultSetRepository resultSetRepository,
    ISettingsSnapshotSource settingsSnapshotSource,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateResultRulesCommand, Result<ResultRulesDto>>
{
    /// <summary>Stable error code for a stale <see cref="SchoolProfile.ResultRulesVersionNumber"/> save.</summary>
    public const string StaleVersionErrorCode = "settings.resultrules.stale_version";

    /// <summary>
    /// Stable error code for both 6.2.10 hard locks (orchestrator ruling 2026-09-18 — see the type
    /// remarks for why this replaced the card's original unprefixed literal).
    /// </summary>
    public const string LockedErrorCode = "settings.resultrules.locked";

    /// <summary>Stable error code for a submitted core-subject id that does not resolve to an existing, active subject.</summary>
    public const string UnknownSubjectIdErrorCode = "settings.resultrules.unknown_subject_id";

    private const string ResultRulesEntityType = "result_rules";

    private const string SchoolProfileEntityType = "school_profile";

    /// <inheritdoc />
    public async Task<Result<ResultRulesDto>> HandleAsync(UpdateResultRulesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resultRules = await resultRulesRepository
            .GetTrackedSingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        var profile = await schoolProfileRepository
            .GetTrackedSingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var resultRulesIdText = resultRules.Id.ToString("D", CultureInfo.InvariantCulture);
        var profileIdText = profile.Id.ToString("D", CultureInfo.InvariantCulture);

        if (profile.ResultRulesVersionNumber != request.ExpectedVersion)
        {
            await auditSink.RecordRejectionAsync(
                "settings.resultrules.save_rejected_stale_version",
                SchoolProfileEntityType,
                profileIdText,
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["expectedVersion"] = request.ExpectedVersion,
                    ["currentVersion"] = profile.ResultRulesVersionNumber,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<ResultRulesDto>(Error.Conflict(
                StaleVersionErrorCode,
                "The result rules were changed by another administrator while you were editing. " +
                "Reload and make your change again."));
        }

        var reasonCheck = await PublishedResultsReasonGate
            .RequireReasonIfPublishedAsync(request.Reason, academicSessionRepository, publishedResultsGate, cancellationToken)
            .ConfigureAwait(false);
        if (reasonCheck.IsFailure)
        {
            return Result.Failure<ResultRulesDto>(reasonCheck.Error);
        }

        if (request.CoreSubjectIds.Count > 0)
        {
            var subjects = await subjectRepository.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
            var activeSubjectIds = subjects
                .Where(subject => subject.Status == SubjectStatus.Active)
                .Select(subject => subject.Id)
                .ToHashSet();

            foreach (var subjectId in request.CoreSubjectIds)
            {
                if (!activeSubjectIds.Contains(subjectId))
                {
                    return Result.Failure<ResultRulesDto>(Error.Validation(
                        UnknownSubjectIdErrorCode,
                        Invariant($"Subject {subjectId} does not exist or is not active.")));
                }
            }
        }

        var activeSession = await academicSessionRepository.FindActiveAsync(cancellationToken).ConfigureAwait(false);
        if (activeSession is not null)
        {
            var scopeChanged = request.PrimaryPositionScope != resultRules.PrimaryPositionScope;
            var tieBreakChanged = request.TieBreakRule != resultRules.TieBreakRule;

            if (scopeChanged || tieBreakChanged)
            {
                var publishedCount = await publishedResultsGate
                    .CountPublishedInSessionAsync(activeSession.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (publishedCount > 0)
                {
                    var lockedField = scopeChanged ? "Position scope" : "Tie-break rule";
                    return Result.Failure<ResultRulesDto>(Error.Conflict(
                        LockedErrorCode,
                        Invariant($"{lockedField} is locked. Results are already published in {activeSession.Name}.")));
                }
            }

            var annualMethodChanged = request.AnnualMethod != resultRules.AnnualMethod;
            var weightsChanged =
                request.WeightFirst != resultRules.WeightFirst ||
                request.WeightSecond != resultRules.WeightSecond ||
                request.WeightThird != resultRules.WeightThird;

            if (annualMethodChanged || weightsChanged)
            {
                var thirdTermPublished = await publishedResultsGate
                    .AnyThirdTermPublishedInSessionAsync(activeSession.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (thirdTermPublished)
                {
                    var lockedField = annualMethodChanged ? "Annual computation method" : "Term weights";
                    return Result.Failure<ResultRulesDto>(Error.Conflict(
                        LockedErrorCode,
                        Invariant($"{lockedField} is locked. Third Term has been published for an arm in {activeSession.Name}.")));
                }
            }
        }

        var beforeMetadata = BuildMetadata(resultRules);

        resultRules.Update(
            request.AnnualMethod,
            request.WeightFirst,
            request.WeightSecond,
            request.WeightThird,
            request.PrimaryPositionScope,
            request.ShowLevelPosition,
            request.TieBreakRule,
            request.PassMark,
            request.PromotionThreshold,
            request.RequireCorePass,
            request.CoreSubjectIds,
            request.MinSubjectsForPosition);

        profile.IncrementResultRulesVersion();

        var afterMetadata = BuildMetadata(resultRules);

        var snapshotState = await settingsSnapshotSource.LoadAsync(cancellationToken).ConfigureAwait(false);

        var configVersion = ConfigVersion.Create(
            Guid.CreateVersion7(),
            SettingsSnapshotBuilder.Build(profile, snapshotState with { ResultRules = resultRules }),
            ConfigVersionGroup.ResultRules,
            currentUser.UserId,
            reason: reasonCheck.Value,
            now);

        await configVersionRepository.AddAsync(configVersion, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            "settings.resultrules.updated",
            ResultRulesEntityType,
            resultRulesIdText,
            afterMetadata,
            actorAdminId: currentUser.UserId,
            cancellationToken,
            reason: reasonCheck.Value,
            beforeMetadata: beforeMetadata).ConfigureAwait(false);

        // TASK-0088 AC A1: flags every non-Published result set in the active session already
        // resolved above for the two 6.2.10 hard locks — no second lookup needed.
        if (activeSession is not null)
        {
            var lockedResultSets = await resultSetRepository
                .LockNonPublishedInSessionAsync(activeSession.Id, cancellationToken)
                .ConfigureAwait(false);

            await ResultSetRecomputeFlagger.FlagAsync(lockedResultSets, auditSink, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(SettingsMapper.ToResultRulesDto(resultRules, profile.ResultRulesVersionNumber));
    }

    private static Dictionary<string, object?> BuildMetadata(ResultRules resultRules) => new(StringComparer.Ordinal)
    {
        ["annualMethod"] = resultRules.AnnualMethod.ToString(),
        ["weightFirst"] = resultRules.WeightFirst,
        ["weightSecond"] = resultRules.WeightSecond,
        ["weightThird"] = resultRules.WeightThird,
        ["primaryPositionScope"] = resultRules.PrimaryPositionScope.ToString(),
        ["showLevelPosition"] = resultRules.ShowLevelPosition,
        ["tieBreakRule"] = resultRules.TieBreakRule.ToString(),
        ["passMark"] = resultRules.PassMark,
        ["promotionThreshold"] = resultRules.PromotionThreshold,
        ["requireCorePass"] = resultRules.RequireCorePass,
        ["coreSubjectIds"] = resultRules.CoreSubjectIds.Select(id => id.ToString("D", CultureInfo.InvariantCulture)).ToList(),
        ["minSubjectsForPosition"] = resultRules.MinSubjectsForPosition,
    };

    private static string Invariant(FormattableString formattable) =>
        formattable.ToString(CultureInfo.InvariantCulture);
}
