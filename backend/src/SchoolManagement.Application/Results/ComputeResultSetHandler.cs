using System.Globalization;
using System.Text.Json;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Results.Computation;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="ComputeResultSetCommand"/>.</summary>
/// <remarks>
/// Loads plain inputs, calls the pure <see cref="ResultComputationEngine"/>, and persists the result
/// inside one transaction (spec 8.2 step 11) — the "handler loads, calls it, persists" split the card
/// names explicitly. Human ruling 1 (2026-09-17): level position is computed here, in memory, from
/// every sibling arm's LIVE marks — this handler never reads or writes a sibling's stored computed
/// rows, only its live <c>subject_score</c> data, and never touches a sibling's own <c>result_set</c>.
/// </remarks>
internal sealed class ComputeResultSetHandler(
    IResultSetRepository resultSets,
    IArmRepository arms,
    IEnrolmentRepository enrolments,
    SubjectsInEffectResolver subjectsInEffect,
    IAssessmentComponentRepository components,
    IGradingBandRepository gradingBands,
    IResultRulesRepository resultRules,
    ISubjectScoreRepository scores,
    IResultComputationRepository computedRows,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<ComputeResultSetCommand, Result<ComputeResultSetResponse>>
{
    /// <summary>Spec 6.7.11: computation is permitted in Draft, Awaiting Approval, Approved and Returned for Correction — never Published or Withdrawn.</summary>
    private static readonly ResultSetState[] BlockedStates = [ResultSetState.Published, ResultSetState.Withdrawn];

    /// <inheritdoc />
    public async Task<Result<ComputeResultSetResponse>> HandleAsync(
        ComputeResultSetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // TASK-0088 AC A4: row-locked before the state check below, so a concurrent settings/mapping
        // flag cannot commit between this read and MarkComputed's clear of NeedsRecompute.
        var resultSet = await resultSets.FindTrackedByIdForUpdateAsync(request.ResultSetId, cancellationToken).ConfigureAwait(false);
        if (resultSet is null)
        {
            return Result.Failure<ComputeResultSetResponse>(Error.NotFound(
                "result_set.not_found", "No result set was found with that id."));
        }

        if (BlockedStates.Contains(resultSet.State))
        {
            return Result.Failure<ComputeResultSetResponse>(Error.Conflict(
                "result_set.published", $"This result set is {resultSet.State} and cannot be recomputed."));
        }

        var arm = await arms.FindReadOnlyByIdAsync(resultSet.ArmId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<ComputeResultSetResponse>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var rules = await resultRules.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var writeLevelPosition = rules.ShowLevelPosition || rules.PrimaryPositionScope == PrimaryPositionScope.Level;

        var thisArmInput = await LoadArmInputAsync(arm.Id, resultSet.TermId, cancellationToken).ConfigureAwait(false);
        if (thisArmInput.IsFailure)
        {
            return Result.Failure<ComputeResultSetResponse>(thisArmInput.Error);
        }

        var componentList = (await components.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false))
            .Select(component => new ComputationComponent(component.Id, component.IsExamination))
            .ToList();

        var bandList = (await gradingBands.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false))
            .Select(band => new ComputationGradingBand(band.LowerBound, band.UpperBound, band.GradeLetter, band.Remark))
            .ToList();

        List<ArmMarksInput>? siblingInputs = null;
        if (writeLevelPosition)
        {
            siblingInputs = await LoadSiblingArmInputsAsync(arm, resultSet.TermId, cancellationToken).ConfigureAwait(false);
        }

        var engineRules = new ComputationRules(rules.TieBreakRule, rules.PassMark, rules.MinSubjectsForPosition);
        var engineInput = new ComputeResultSetInput(
            thisArmInput.Value, componentList, bandList, engineRules, writeLevelPosition, siblingInputs);

        var computed = ResultComputationEngine.Compute(engineInput);
        if (computed.IsFailure)
        {
            return Result.Failure<ComputeResultSetResponse>(computed.Error);
        }

        var output = computed.Value;
        var now = timeProvider.GetUtcNow();

        var subjectLineEntities = output.SubjectLines
            .Select(line => SubjectResultLine.Create(
                Guid.CreateVersion7(), resultSet.Id, line.PupilId, line.SubjectId, line.CaTotal, line.ExamMark,
                line.SubjectTotal, line.Grade, line.Remark, line.SubjectPosition, line.SubjectPositionTied, line.IsPass))
            .ToList();

        var subjectStatisticEntities = output.SubjectStatistics
            .Select(stat => SubjectArmStatistic.Create(
                Guid.CreateVersion7(), resultSet.Id, stat.SubjectId, stat.HighestScore, stat.LowestScore,
                stat.ClassAverage, stat.CountedPupils, stat.RankedPupils))
            .ToList();

        var pupilResultEntities = output.PupilResults
            .Select(pupil => PupilTermResult.Create(
                Guid.CreateVersion7(), resultSet.Id, pupil.PupilId, pupil.SubjectsTaken, pupil.TotalObtainable,
                pupil.TotalObtained, pupil.Average, pupil.OverallGrade, pupil.ArmPosition, pupil.ArmPositionTied,
                pupil.ArmPupilCount, pupil.LevelPosition, pupil.LevelPositionTied, pupil.LevelPupilCount))
            .ToList();

        await computedRows.ReplaceComputedRowsAsync(
            resultSet.Id, subjectLineEntities, subjectStatisticEntities, pupilResultEntities, cancellationToken)
            .ConfigureAwait(false);

        // "Number of pupils ranked" (spec 6.7.3) — the denominator printed on the sheet, so the
        // min_subjects_for_position-excluded pupils (null ArmPosition) do not inflate it.
        var pupilCount = output.PupilResults.Count(pupil => pupil.ArmPosition is not null);
        var computedBy = currentUser.UserId is { } userId ? Guid.Parse(userId) : (Guid?)null;
        resultSet.MarkComputed(computedBy, now, pupilCount);

        await auditSink.RecordAsync(
            Privileges.Results.Compute,
            "result_set",
            resultSet.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pupilCount"] = pupilCount,
                ["subjectCount"] = thisArmInput.Value.Subjects.Count,
                ["subjectLineCount"] = subjectLineEntities.Count,
                ["flagCount"] = output.Flags.Count,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        var flagDtos = output.Flags
            .Select(flag => new ComputeResultSetFlagDto(
                flag.Code,
                flag.SubjectId?.ToString("D", CultureInfo.InvariantCulture),
                flag.PupilId?.ToString("D", CultureInfo.InvariantCulture)))
            .ToList();

        return Result.Success(new ComputeResultSetResponse(
            resultSet.Id.ToString("D", CultureInfo.InvariantCulture),
            now,
            pupilCount,
            thisArmInput.Value.Subjects.Count,
            flagDtos));
    }

    /// <summary>
    /// Every OTHER arm at <paramref name="arm"/>'s class level and session (human ruling 1: "Rank the
    /// whole level from every arm's live... marks"). A sibling with no subjects in effect, or with no
    /// roster, is simply omitted (<see cref="LoadArmInputAsync"/> never fails for either case) — it
    /// contributes no pupils to the level ranking, exactly as spec 6.7.12's "arm with zero active
    /// pupils" reads when applied to a class nobody has started yet.
    /// </summary>
    private async Task<List<ArmMarksInput>> LoadSiblingArmInputsAsync(
        Arm arm, Guid termId, CancellationToken cancellationToken)
    {
        var siblingArms = (await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false))
            .Where(candidate => candidate.ClassLevelId == arm.ClassLevelId
                && candidate.SessionId == arm.SessionId
                && candidate.Id != arm.Id)
            .ToList();

        var siblings = new List<ArmMarksInput>(siblingArms.Count);
        foreach (var siblingArm in siblingArms)
        {
            var siblingInput = await LoadArmInputAsync(siblingArm.Id, termId, cancellationToken).ConfigureAwait(false);
            if (siblingInput.IsSuccess)
            {
                siblings.Add(siblingInput.Value);
            }
        }

        return siblings;
    }

    /// <summary>
    /// Loads one arm's roster, subjects in effect and live marks (spec 8.1) into the engine's input
    /// shape. Used for both <c>ThisArm</c> and every sibling arm (human ruling 1) — a sibling with no
    /// <c>result_set</c> row yet (nobody has entered a mark) simply has an empty <see cref="ArmMarksInput.Marks"/>.
    /// </summary>
    private async Task<Result<ArmMarksInput>> LoadArmInputAsync(Guid armId, Guid termId, CancellationToken cancellationToken)
    {
        var subjectsResolution = await subjectsInEffect.ResolveAsync(armId, termId, cancellationToken).ConfigureAwait(false);
        if (subjectsResolution.IsFailure)
        {
            return Result.Failure<ArmMarksInput>(subjectsResolution.Error);
        }

        var roster = await enrolments.ListActiveRosterByArmAsync(armId, cancellationToken).ConfigureAwait(false);
        var pupils = roster
            .Select(pupil => new ComputationPupil(pupil.PupilId, pupil.RegistrationNumber, pupil.Surname))
            .ToList();

        var subjectList = subjectsResolution.Value
            .Select(subject => new ComputationSubject(subject.SubjectId, subject.SubjectName))
            .ToList();

        var existingResultSet = await resultSets.FindReadOnlyByArmTermAsync(armId, termId, cancellationToken).ConfigureAwait(false);
        var marks = existingResultSet is null
            ? []
            : await scores.ListAllActiveReadOnlyAsync(existingResultSet.Id, cancellationToken).ConfigureAwait(false);

        var markRows = marks
            .Select(mark => new ComputationMarkRow(
                mark.PupilId, mark.SubjectId, DeserializeComponentMarks(mark.ComponentMarksJson), mark.ExamMark, mark.ExamAbsent))
            .ToList();

        return Result.Success(new ArmMarksInput(armId, pupils, subjectList, markRows));
    }

    /// <summary>
    /// <c>SubjectScore.ComponentMarksJson</c> is stored with STRING keys (JSON object keys are always
    /// strings, and the wire type is <c>IReadOnlyDictionary&lt;string, int?&gt;</c> — see
    /// <c>SaveScoreSheetHandler</c>) even though each key is a component id. Parsed explicitly here
    /// rather than deserialising straight into <c>Dictionary&lt;Guid, int?&gt;</c>, which would depend
    /// on System.Text.Json's non-string dictionary-key support rather than this codebase's own
    /// documented convention.
    /// </summary>
    private static Dictionary<Guid, int?> DeserializeComponentMarks(string json)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, int?>>(json) ?? [];
        var result = new Dictionary<Guid, int?>(raw.Count);

        foreach (var (key, value) in raw)
        {
            if (Guid.TryParse(key, out var componentId))
            {
                result[componentId] = value;
            }
        }

        return result;
    }
}
