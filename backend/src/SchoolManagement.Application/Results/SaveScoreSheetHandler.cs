using System.Globalization;
using System.Text.Json;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="SaveScoreSheetCommand"/>.</summary>
/// <remarks>
/// <c>result.score.enter</c> is the route's ONE declarative privilege (arm-scoped) — spec 6.7.2 also
/// requires the result set to be Draft or Returned for Correction, which is DATA-DEPENDENT (needs the
/// existing result set's state) and so is enforced here as a 409, the same "route declares the
/// baseline, handler enforces the data-dependent rest" split <c>SaveSubjectMappingGridHandler</c> uses.
/// </remarks>
internal sealed class SaveScoreSheetHandler(
    IArmRepository arms,
    IAcademicSessionRepository sessions,
    ITermRepository terms,
    ISubjectRepository subjects,
    SubjectsInEffectResolver subjectsInEffect,
    IAssessmentComponentRepository components,
    IEnrolmentRepository enrolments,
    IResultSetRepository resultSets,
    ISubjectScoreRepository scores,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<SaveScoreSheetCommand, Result<ScoreSheetDto>>
{
    /// <inheritdoc />
    public async Task<Result<ScoreSheetDto>> HandleAsync(SaveScoreSheetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var armId = Guid.Parse(request.ArmId);
        var subjectId = Guid.Parse(request.SubjectId);
        var termId = Guid.Parse(request.TermId);

        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<ScoreSheetDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<ScoreSheetDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        if (term.SessionId != arm.SessionId)
        {
            return Result.Failure<ScoreSheetDto>(Error.Validation(
                "score_sheet.term_session_mismatch", "The term must belong to the arm's session."));
        }

        var subject = await subjects.FindReadOnlyByIdAsync(subjectId, cancellationToken).ConfigureAwait(false);
        if (subject is null)
        {
            return Result.Failure<ScoreSheetDto>(Error.NotFound("subject.not_found", "No subject was found with that id."));
        }

        var resolution = await subjectsInEffect.ResolveAsync(armId, termId, cancellationToken).ConfigureAwait(false);
        if (resolution.IsFailure)
        {
            return Result.Failure<ScoreSheetDto>(resolution.Error);
        }

        if (!resolution.Value.Any(resolved => resolved.SubjectId == subjectId))
        {
            return Result.Failure<ScoreSheetDto>(Error.Validation(
                "score_sheet.subject_not_in_effect", $"{subject.Name} is not in effect for this arm this term."));
        }

        var session = await sessions.FindReadOnlyByIdAsync(arm.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is { State: SessionState.Closed })
        {
            return Result.Failure<ScoreSheetDto>(Error.Conflict(
                "score_sheet.session_closed", "This arm's session is closed. Marks cannot be entered."));
        }

        if (term.State == TermState.Closed)
        {
            return Result.Failure<ScoreSheetDto>(Error.Conflict(
                "score_sheet.term_closed", $"{term.Name} is closed. Marks cannot be entered."));
        }

        var roster = await enrolments.ListActiveRosterByArmAsync(armId, cancellationToken).ConfigureAwait(false);
        var rosterPupilIds = roster.Select(pupil => pupil.PupilId).ToHashSet();

        var structure = await components.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var nonExamComponents = structure.Where(component => !component.IsExamination)
            .OrderBy(component => component.DisplayOrder)
            .ToArray();
        var examComponent = structure.Single(component => component.IsExamination);

        var failures = ValidateRows(request.Rows, nonExamComponents, examComponent, rosterPupilIds);
        if (failures.Count > 0)
        {
            return Result.Failure<ScoreSheetDto>(new ValidationError(failures));
        }

        // TASK-0088 AC A4: row-locked before the state check below.
        var existingResultSet = await resultSets.FindTrackedByArmTermForUpdateAsync(armId, termId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SubjectScore> existingScores = existingResultSet is null
            ? []
            : await scores.ListActiveTrackedAsync(existingResultSet.Id, subjectId, cancellationToken).ConfigureAwait(false);

        var currentVersion = ScoreSheetVersion.Compute(existingScores
            .Select(score => new ScoreSheetVersionRow(score.PupilId, score.ComponentMarksJson, score.ExamMark, score.ExamAbsent))
            .ToArray());

        // Locked-state check runs BEFORE the staleness check: a caller who is not allowed to edit this
        // result set at all must be told that first, not sent chasing a version conflict on a set they
        // could never save to regardless of which version they hold.
        if (existingResultSet is not null &&
            existingResultSet.State is not (ResultSetState.Draft or ResultSetState.ReturnedForCorrection))
        {
            return Result.Failure<ScoreSheetDto>(Error.Conflict(
                "score_sheet.result_set_locked", $"This result set is {existingResultSet.State} and marks cannot be edited."));
        }

        if (!string.Equals(request.Version, currentVersion, StringComparison.Ordinal))
        {
            return Result.Failure<ScoreSheetDto>(Error.Conflict(
                "score_sheet.stale_version",
                "This sheet was changed since you last read it. Reload it before saving again."));
        }

        var existingByPupil = existingScores.ToDictionary(score => score.PupilId);
        var finalScoresByPupil = existingScores.ToDictionary(
            score => score.PupilId,
            score => new ScoreSheetVersionRow(score.PupilId, score.ComponentMarksJson, score.ExamMark, score.ExamAbsent));

        var beforeRows = new List<object?>();
        var afterRows = new List<object?>();
        var resultSetRef = existingResultSet;

        var rowsByPupil = new Dictionary<Guid, SaveScoreSheetRowInput>();
        foreach (var row in request.Rows)
        {
            rowsByPupil[Guid.Parse(row.PupilId)] = row;
        }

        foreach (var (pupilId, row) in rowsByPupil)
        {
            var isBlank = row.ComponentMarks!.Values.All(value => value is null) && row.ExamMark is null && !row.ExamAbsent;
            var hasExisting = existingByPupil.TryGetValue(pupilId, out var existingScore);

            if (isBlank)
            {
                if (hasExisting)
                {
                    beforeRows.Add(SnapshotFor(pupilId, existingScore!));
                    await scores.RemoveAsync(existingScore!, cancellationToken).ConfigureAwait(false);
                    finalScoresByPupil.Remove(pupilId);
                }

                continue;
            }

            var componentMarksJson = JsonSerializer.Serialize(row.ComponentMarks);

            if (hasExisting)
            {
                beforeRows.Add(SnapshotFor(pupilId, existingScore!));

                var update = existingScore!.UpdateMarks(componentMarksJson, row.ExamMark, row.ExamAbsent);
                if (update.IsFailure)
                {
                    return Result.Failure<ScoreSheetDto>(update.Error);
                }
            }
            else
            {
                if (resultSetRef is null)
                {
                    var creation = ResultSet.Create(Guid.CreateVersion7(), armId, termId);
                    if (creation.IsFailure)
                    {
                        return Result.Failure<ScoreSheetDto>(creation.Error);
                    }

                    resultSetRef = creation.Value;
                    await resultSets.AddAsync(resultSetRef, cancellationToken).ConfigureAwait(false);
                }

                var scoreCreation = SubjectScore.Create(
                    Guid.CreateVersion7(), resultSetRef.Id, pupilId, subjectId, termId, componentMarksJson, row.ExamMark, row.ExamAbsent);
                if (scoreCreation.IsFailure)
                {
                    return Result.Failure<ScoreSheetDto>(scoreCreation.Error);
                }

                await scores.AddAsync(scoreCreation.Value, cancellationToken).ConfigureAwait(false);
            }

            afterRows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pupilId"] = pupilId.ToString("D", CultureInfo.InvariantCulture),
                ["componentMarks"] = row.ComponentMarks,
                ["examMark"] = row.ExamMark,
                ["examAbsent"] = row.ExamAbsent,
            });
            finalScoresByPupil[pupilId] = new ScoreSheetVersionRow(pupilId, componentMarksJson, row.ExamMark, row.ExamAbsent);
        }

        resultSetRef?.MarkNeedsRecompute();

        var afterMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["armId"] = request.ArmId,
            ["subjectId"] = request.SubjectId,
            ["termId"] = request.TermId,
            ["resultSetId"] = resultSetRef?.Id.ToString("D", CultureInfo.InvariantCulture),
            ["rows"] = afterRows,
        };
        var beforeMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["armId"] = request.ArmId,
            ["subjectId"] = request.SubjectId,
            ["termId"] = request.TermId,
            ["rows"] = beforeRows,
        };

        await auditSink.RecordAsync(
            Privileges.Results.ScoreEnter,
            "subject_score",
            entityId: null,
            afterMetadata,
            currentUser.UserId,
            cancellationToken,
            reason: null,
            beforeMetadata: beforeMetadata).ConfigureAwait(false);

        var dto = ScoreSheetProjection.Build(armId, subjectId, termId, resultSetRef, roster, structure, finalScoresByPupil);

        return Result.Success(dto);
    }

    private static Dictionary<string, object?> SnapshotFor(Guid pupilId, SubjectScore score) => new(StringComparer.Ordinal)
    {
        ["pupilId"] = pupilId.ToString("D", CultureInfo.InvariantCulture),
        ["componentMarks"] = JsonSerializer.Deserialize<Dictionary<string, int?>>(score.ComponentMarksJson),
        ["examMark"] = score.ExamMark,
        ["examAbsent"] = score.ExamAbsent,
    };

    private static Dictionary<string, string[]> ValidateRows(
        IReadOnlyList<SaveScoreSheetRowInput> rows,
        AssessmentComponent[] nonExamComponents,
        AssessmentComponent examComponent,
        HashSet<Guid> rosterPupilIds)
    {
        var failures = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var knownKeys = nonExamComponents
            .ToDictionary(component => component.Id.ToString("D", CultureInfo.InvariantCulture), component => component);

        void AddFailure(string path, string message)
        {
            if (!failures.TryGetValue(path, out var list))
            {
                list = [];
                failures[path] = list;
            }

            if (!list.Contains(message, StringComparer.Ordinal))
            {
                list.Add(message);
            }
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];

            if (!Guid.TryParse(row.PupilId, out var pupilId) || !rosterPupilIds.Contains(pupilId))
            {
                AddFailure($"Rows[{index}].PupilId", "This pupil is not on the arm's active roster.");
                continue;
            }

            var marks = row.ComponentMarks ?? new Dictionary<string, int?>(StringComparer.Ordinal);

            foreach (var (key, component) in knownKeys)
            {
                if (!marks.ContainsKey(key))
                {
                    AddFailure($"Rows[{index}].ComponentMarks[{key}]", $"{component.ShortLabel} is required for every row.");
                }
            }

            foreach (var (key, value) in marks)
            {
                if (!knownKeys.TryGetValue(key, out var component))
                {
                    AddFailure($"Rows[{index}].ComponentMarks[{key}]", "This is not part of the assessment structure.");
                    continue;
                }

                if (value is null)
                {
                    continue;
                }

                if (value < 0 || value > component.MaxMark)
                {
                    AddFailure($"Rows[{index}].ComponentMarks[{key}]", $"Maximum for {component.ShortLabel} is {component.MaxMark}.");
                }
            }

            if (row.ExamAbsent && row.ExamMark is not null)
            {
                AddFailure($"Rows[{index}].ExamMark", "A pupil cannot be marked absent and given an exam mark.");
            }
            else if (row.ExamMark is { } examMark && (examMark < 0 || examMark > examComponent.MaxMark))
            {
                AddFailure($"Rows[{index}].ExamMark", $"Maximum for {examComponent.ShortLabel} is {examComponent.MaxMark}.");
            }
        }

        return failures.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal);
    }
}
