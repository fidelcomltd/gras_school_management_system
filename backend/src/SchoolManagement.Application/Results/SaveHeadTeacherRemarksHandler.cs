using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="SaveHeadTeacherRemarksCommand"/>.</summary>
/// <remarks>
/// <c>result.remark.headteacher</c> is the route's ONE declarative privilege (school-wide, NOT
/// arm-scoped — spec 6.7.2) — spec's ruling H (2026-09-19) also requires the result set to be in
/// Draft, Returned for Correction, Awaiting Approval or Approved (a WIDER window than the
/// class-teacher and attendance sheets, because the head teacher writes this remark on the
/// approval screen, after submission), both DATA-DEPENDENT, so both are enforced here.
/// </remarks>
internal sealed class SaveHeadTeacherRemarksHandler(
    IArmRepository arms,
    IAcademicSessionRepository sessions,
    ITermRepository terms,
    IEnrolmentRepository enrolments,
    IResultSetRepository resultSets,
    IPupilRemarkRepository pupilRemarks,
    IAdminAccountRepository accounts,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<SaveHeadTeacherRemarksCommand, Result<RemarkSheetDto>>
{
    /// <summary>The result set is Published or Withdrawn (ruling H, 2026-09-19).</summary>
    public const string ResultSetLockedErrorCode = "head_teacher_remarks.result_set_locked";

    /// <summary>The submitted <c>version</c> does not match the sheet's current version.</summary>
    public const string StaleVersionErrorCode = "head_teacher_remarks.stale_version";

    private static readonly ResultSetState[] LockedStates = [ResultSetState.Published, ResultSetState.Withdrawn];

    /// <inheritdoc />
    public async Task<Result<RemarkSheetDto>> HandleAsync(SaveHeadTeacherRemarksCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } actorIdText || !Guid.TryParse(actorIdText, out var actorId))
        {
            return Result.Failure<RemarkSheetDto>(Error.Unauthenticated(
                "authentication.required", "Sign in to perform this action."));
        }

        var armId = Guid.Parse(request.ArmId);
        var termId = Guid.Parse(request.TermId);

        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<RemarkSheetDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<RemarkSheetDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        if (term.SessionId != arm.SessionId)
        {
            return Result.Failure<RemarkSheetDto>(Error.Validation(
                "head_teacher_remarks.term_session_mismatch", "The term must belong to the arm's session."));
        }

        var session = await sessions.FindReadOnlyByIdAsync(arm.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is { State: SessionState.Closed })
        {
            return Result.Failure<RemarkSheetDto>(Error.Conflict(
                "head_teacher_remarks.session_closed", "This arm's session is closed. Remarks cannot be entered."));
        }

        if (term.State == TermState.Closed)
        {
            return Result.Failure<RemarkSheetDto>(Error.Conflict(
                "head_teacher_remarks.term_closed", $"{term.Name} is closed. Remarks cannot be entered."));
        }

        var roster = await enrolments.ListActiveRosterByArmAsync(armId, cancellationToken).ConfigureAwait(false);
        var rosterPupilIds = roster.Select(pupil => pupil.PupilId).ToHashSet();

        var failures = PupilRemarkSaveEngine.ValidateRows(request.Rows, rosterPupilIds);
        if (failures.Count > 0)
        {
            return Result.Failure<RemarkSheetDto>(new ValidationError(failures));
        }

        var existingResultSet = await resultSets.FindTrackedByArmTermAsync(armId, termId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PupilRemark> existingRemarks = existingResultSet is null
            ? []
            : await pupilRemarks.ListTrackedAsync(existingResultSet.Id, RemarkKind.HeadTeacher, cancellationToken).ConfigureAwait(false);

        var currentVersion = RemarkVersion.Compute(existingRemarks
            .Select(remark => new PupilRemarkSnapshot(remark.PupilId, remark.Text, remark.WrittenByName, remark.WrittenAtUtc))
            .ToArray());

        // Locked-state check runs BEFORE the staleness check — same ordering rule as SaveTraitRatingsHandler.
        if (existingResultSet is not null && LockedStates.Contains(existingResultSet.State))
        {
            return Result.Failure<RemarkSheetDto>(Error.Conflict(
                ResultSetLockedErrorCode, $"This result set is {existingResultSet.State} and the head teacher's remark cannot be edited."));
        }

        if (!string.Equals(request.Version, currentVersion, StringComparison.Ordinal))
        {
            return Result.Failure<RemarkSheetDto>(Error.Conflict(
                StaleVersionErrorCode, "This sheet was changed since you last read it. Reload it before saving again."));
        }

        var actor = await accounts.FindReadOnlyByIdAsync(actorId, cancellationToken).ConfigureAwait(false);
        if (actor is null)
        {
            return Result.Failure<RemarkSheetDto>(Error.Failure(
                "head_teacher_remarks.actor_not_found", "The signed-in account could not be found."));
        }

        var byPupil = existingRemarks.ToDictionary(remark => remark.PupilId);
        var beforeChanges = new List<object?>();
        var afterChanges = new List<object?>();
        var writtenAt = timeProvider.GetUtcNow();

        var application = await PupilRemarkSaveEngine.ApplyAsync(
            request.Rows, byPupil, existingResultSet, armId, termId, RemarkKind.HeadTeacher,
            actorId, actor.StaffName, writtenAt, resultSets, pupilRemarks,
            beforeChanges, afterChanges, cancellationToken).ConfigureAwait(false);

        if (application.IsFailure)
        {
            return Result.Failure<RemarkSheetDto>(application.Error);
        }

        var resultSetRef = application.Value;

        var trimmedFill = request.FillEmpty?.Trim();
        if (!string.IsNullOrEmpty(trimmedFill))
        {
            var fillApplication = await PupilRemarkSaveEngine.FillEmptyAsync(
                trimmedFill, rosterPupilIds, byPupil, resultSetRef, armId, termId, RemarkKind.HeadTeacher,
                actorId, actor.StaffName, writtenAt, resultSets, pupilRemarks,
                beforeChanges, afterChanges, cancellationToken).ConfigureAwait(false);

            if (fillApplication.IsFailure)
            {
                return Result.Failure<RemarkSheetDto>(fillApplication.Error);
            }

            resultSetRef = fillApplication.Value;
        }

        if (afterChanges.Count > 0)
        {
            // Remark TEXT must stay out of logs (standing obligation) — only which pupils changed is
            // recorded, never what was written. Both before_json and after_json are still populated,
            // just with the pupil-id list rather than the content.
            var affectedPupilIds = beforeChanges
                .Select(entry => ((Dictionary<string, object?>)entry!)["pupilId"])
                .ToArray();

            await auditSink.RecordAsync(
                Privileges.Results.RemarkHeadTeacher,
                "pupil_remark",
                entityId: null,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["armId"] = request.ArmId, ["termId"] = request.TermId, ["kind"] = nameof(RemarkKind.HeadTeacher), ["pupilIds"] = affectedPupilIds },
                currentUser.UserId,
                cancellationToken,
                reason: null,
                beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["armId"] = request.ArmId, ["termId"] = request.TermId, ["kind"] = nameof(RemarkKind.HeadTeacher), ["pupilIds"] = affectedPupilIds })
                .ConfigureAwait(false);
        }

        var finalRemarks = byPupil.Values
            .Select(remark => new PupilRemarkSnapshot(remark.PupilId, remark.Text, remark.WrittenByName, remark.WrittenAtUtc))
            .ToArray();

        var dto = RemarkProjection.Build(armId, termId, resultSetRef, roster, finalRemarks);

        return Result.Success(dto);
    }
}
