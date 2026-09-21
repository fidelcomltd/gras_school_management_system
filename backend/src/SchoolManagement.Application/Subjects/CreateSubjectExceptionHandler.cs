using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>Handles <see cref="CreateSubjectExceptionCommand"/>.</summary>
internal sealed class CreateSubjectExceptionHandler(
    IArmRepository arms,
    IClassLevelRepository levels,
    IAcademicSessionRepository sessions,
    ITermRepository terms,
    ISubjectRepository subjects,
    ISubjectMappingRepository mappings,
    ISubjectMappingExceptionRepository exceptions,
    IResultSetRepository resultSets,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<CreateSubjectExceptionCommand, Result<SubjectExceptionDto>>
{
    private const string EntityType = "subject_mapping_exception";

    /// <inheritdoc />
    public async Task<Result<SubjectExceptionDto>> HandleAsync(
        CreateSubjectExceptionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var armId = Guid.Parse(request.ArmId);
        var subjectId = Guid.Parse(request.SubjectId);
        var termId = Guid.Parse(request.TermId);

        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);

        if (arm is null)
        {
            return Result.Failure<SubjectExceptionDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);

        if (term is null)
        {
            return Result.Failure<SubjectExceptionDto>(Error.Validation("term.not_found", "No term was found with that id."));
        }

        if (term.SessionId != arm.SessionId)
        {
            return Result.Failure<SubjectExceptionDto>(Error.Validation(
                "subject_exception.term_session_mismatch", "The term must belong to the arm's session."));
        }

        var session = await sessions.FindReadOnlyByIdAsync(arm.SessionId, cancellationToken).ConfigureAwait(false);

        if (session is { State: SessionState.Closed })
        {
            var allLevels = await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
            var levelName = allLevels.FirstOrDefault(level => level.Id == arm.ClassLevelId)?.Name ?? "Unknown level";

            return Result.Failure<SubjectExceptionDto>(Error.Conflict(
                "subject_exception.arm_session_closed",
                $"{Domain.Classes.ArmDisplayName.Compose(levelName, arm.Label)}'s session is closed. Subject exceptions cannot be created for it."));
        }

        var subject = await subjects.FindReadOnlyByIdAsync(subjectId, cancellationToken).ConfigureAwait(false);

        if (subject is null || subject.Status != SubjectStatus.Active)
        {
            return Result.Failure<SubjectExceptionDto>(Error.Validation(
                "subject_exception.subject_not_found", "No active subject was found with that id."));
        }

        if (await exceptions.ExistsAsync(armId, subjectId, termId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<SubjectExceptionDto>(Error.Conflict(
                "subject_exception.duplicate", "An exception already exists for this arm, subject and term."));
        }

        var isActivelyMapped = await mappings
            .IsActivelyMappedAsync(subjectId, arm.ClassLevelId, termId, cancellationToken)
            .ConfigureAwait(false);

        var levelsForMessage = await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var mappedLevelName = levelsForMessage.FirstOrDefault(level => level.Id == arm.ClassLevelId)?.Name ?? "this level";

        if (request.Mode == SubjectExceptionMode.Include && isActivelyMapped)
        {
            return Result.Failure<SubjectExceptionDto>(Error.Conflict(
                "subject_exception.redundant_include",
                $"{subject.Name} is already mapped to {mappedLevelName} this term. This arm already takes it."));
        }

        if (request.Mode == SubjectExceptionMode.Exclude && !isActivelyMapped)
        {
            return Result.Failure<SubjectExceptionDto>(Error.Conflict(
                "subject_exception.redundant_exclude",
                $"{subject.Name} is not mapped to {mappedLevelName} this term. This arm does not take it."));
        }

        var creation = SubjectMappingException.Create(
            Guid.CreateVersion7(), armId, subjectId, termId, request.Mode, request.Reason);

        if (creation.IsFailure)
        {
            return Result.Failure<SubjectExceptionDto>(creation.Error);
        }

        var exception = creation.Value;
        await exceptions.AddAsync(exception, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Subject.MapArm,
            EntityType,
            exception.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["armId"] = request.ArmId,
                ["subjectId"] = request.SubjectId,
                ["termId"] = request.TermId,
                ["mode"] = exception.Mode.ToString(),
                ["reason"] = exception.Reason,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        // TASK-0088 AC A2: an include/exclude exception changes exactly this ONE arm's in-effect
        // subject list — flags its result set for this term, if one exists and is not Published
        // (spec 6.7.11: "A Published set is never flagged, because it renders from its snapshot").
        var lockedResultSet = await resultSets
            .FindTrackedByArmTermForUpdateAsync(armId, termId, cancellationToken)
            .ConfigureAwait(false);

        if (lockedResultSet is { State: not ResultSetState.Published })
        {
            await ResultSetRecomputeFlagger
                .FlagAsync([lockedResultSet], auditSink, cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Success(new SubjectExceptionDto(
            exception.Id.ToString("D", CultureInfo.InvariantCulture),
            request.ArmId,
            request.SubjectId,
            subject.Name,
            request.TermId,
            exception.Mode,
            exception.Reason));
    }
}
