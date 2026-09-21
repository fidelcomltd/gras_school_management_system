using System.Globalization;
using System.Text.Json;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="VoidScoreSheetCommand"/>.</summary>
/// <remarks>
/// Applies the same lock-state guard as <c>SaveScoreSheetHandler</c> (Draft or Returned for
/// Correction only) — spec 6.7.4 calls void "a Super Admin action... used only to unwind an error",
/// which reads as unwinding an entry mistake before it is signed off, not as a bypass of the
/// state-machine lock a Super Admin could use to alter an already-approved or published set.
/// </remarks>
internal sealed class VoidScoreSheetHandler(
    IArmRepository arms,
    ITermRepository terms,
    ISubjectRepository subjects,
    IResultSetRepository resultSets,
    ISubjectScoreRepository scores,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<VoidScoreSheetCommand, Result<VoidScoreSheetResponse>>
{
    /// <inheritdoc />
    public async Task<Result<VoidScoreSheetResponse>> HandleAsync(VoidScoreSheetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var armId = Guid.Parse(request.ArmId);
        var subjectId = Guid.Parse(request.SubjectId);
        var termId = Guid.Parse(request.TermId);

        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<VoidScoreSheetResponse>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<VoidScoreSheetResponse>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var subject = await subjects.FindReadOnlyByIdAsync(subjectId, cancellationToken).ConfigureAwait(false);
        if (subject is null)
        {
            return Result.Failure<VoidScoreSheetResponse>(Error.NotFound("subject.not_found", "No subject was found with that id."));
        }

        // TASK-0088 AC A4: row-locked before the state check below.
        var resultSet = await resultSets.FindTrackedByArmTermForUpdateAsync(armId, termId, cancellationToken).ConfigureAwait(false);
        if (resultSet is null)
        {
            // Nothing has ever been entered for this arm and term — nothing to void.
            return Result.Success(new VoidScoreSheetResponse(0));
        }

        if (resultSet.State is not (ResultSetState.Draft or ResultSetState.ReturnedForCorrection))
        {
            return Result.Failure<VoidScoreSheetResponse>(Error.Conflict(
                "score_sheet.result_set_locked", $"This result set is {resultSet.State} and marks cannot be voided."));
        }

        var activeScores = await scores.ListActiveTrackedAsync(resultSet.Id, subjectId, cancellationToken).ConfigureAwait(false);
        if (activeScores.Count == 0)
        {
            return Result.Success(new VoidScoreSheetResponse(0));
        }

        var voidedAt = timeProvider.GetUtcNow();
        var beforeRows = new List<object?>();
        var voidedPupilIds = new List<string>();

        foreach (var score in activeScores)
        {
            beforeRows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pupilId"] = score.PupilId.ToString("D", CultureInfo.InvariantCulture),
                ["componentMarks"] = JsonSerializer.Deserialize<Dictionary<string, int?>>(score.ComponentMarksJson),
                ["examMark"] = score.ExamMark,
                ["examAbsent"] = score.ExamAbsent,
            });

            var voiding = score.Void(request.Reason, currentUser.UserId, voidedAt);
            if (voiding.IsFailure)
            {
                return Result.Failure<VoidScoreSheetResponse>(voiding.Error);
            }

            voidedPupilIds.Add(score.PupilId.ToString("D", CultureInfo.InvariantCulture));
        }

        resultSet.MarkNeedsRecompute();

        var beforeMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["armId"] = request.ArmId,
            ["subjectId"] = request.SubjectId,
            ["termId"] = request.TermId,
            ["rows"] = beforeRows,
        };
        var afterMetadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["armId"] = request.ArmId,
            ["subjectId"] = request.SubjectId,
            ["termId"] = request.TermId,
            ["voidedCount"] = activeScores.Count,
            ["voidedPupilIds"] = voidedPupilIds,
            ["reason"] = request.Reason,
        };

        await auditSink.RecordAsync(
            Privileges.Results.ScoreVoid,
            "subject_score",
            entityId: null,
            afterMetadata,
            currentUser.UserId,
            cancellationToken,
            reason: request.Reason,
            beforeMetadata: beforeMetadata).ConfigureAwait(false);

        return Result.Success(new VoidScoreSheetResponse(activeScores.Count));
    }
}
