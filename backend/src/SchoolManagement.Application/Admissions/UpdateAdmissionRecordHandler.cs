using System.Globalization;
using SchoolManagement.Application.Abstractions.Admissions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Admissions;

/// <summary>Handles <see cref="UpdateAdmissionRecordCommand"/>.</summary>
/// <remarks>
/// School-wide <c>pupil.update</c> only (see <c>AdmissionEndpoints.MapEndpoints</c>) — a pending
/// pupil has no open enrolment, so an arm-scoped grant could never resolve a target arm here, the
/// exact reasoning <c>ListAdmissionsQueueQuery</c>'s own route already established for the queue read.
/// </remarks>
internal sealed class UpdateAdmissionRecordHandler(
    IAdmissionRecordRepository admissionRecords,
    IPupilRepository pupils,
    IClassLevelRepository classLevels,
    IAcademicSessionRepository sessions,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateAdmissionRecordCommand, Result<AdmissionRecordDto>>
{
    private const string EntityType = "admission_record";

    /// <inheritdoc />
    public async Task<Result<AdmissionRecordDto>> HandleAsync(
        UpdateAdmissionRecordCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // FindReadOnlyByIdAsync ignores the pending-exclusion filter (spec 6.5.14) — direct-id access
        // is never subject to it (PupilRepository's own remarks), which is exactly what this route
        // needs: it exists ONLY to keep editing a pending record.
        var pupil = await pupils.FindReadOnlyByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (pupil is null || pupil.Status != PupilStatus.Pending)
        {
            return Result.Failure<AdmissionRecordDto>(Error.NotFound(
                "admission_record.not_found", "No pending admission was found with that id."));
        }

        var record = await admissionRecords.FindTrackedByPupilIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            // Cannot happen given CreatePupilHandler's same-transaction invariant — defensive, not a
            // reachable branch in normal operation.
            return Result.Failure<AdmissionRecordDto>(Error.NotFound(
                "admission_record.not_found", "No pending admission was found with that id."));
        }

        var allLevels = await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var levelsById = allLevels.ToDictionary(level => level.Id, level => level);

        Guid? resolvedClassAdmittedInto = null;

        if (request.ClassAdmittedInto is not null)
        {
            var levelId = Guid.Parse(request.ClassAdmittedInto);

            if (!levelsById.TryGetValue(levelId, out var level) || level.Status != LevelStatus.Active)
            {
                return Result.Failure<AdmissionRecordDto>(Error.Validation(
                    "admission_record.class_admitted_into_invalid", "ClassAdmittedInto must reference an existing, active class level."));
            }

            resolvedClassAdmittedInto = levelId;
        }

        Guid? resolvedSessionId = null;

        if (request.SessionId is not null)
        {
            var sessionId = Guid.Parse(request.SessionId);
            var session = await sessions.FindReadOnlyByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

            if (session is null)
            {
                return Result.Failure<AdmissionRecordDto>(Error.Validation(
                    "admission_record.session_id_invalid", "SessionId must reference an existing session."));
            }

            resolvedSessionId = sessionId;
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var update = record.Update(
            resolvedSessionId,
            request.DateApplicationReceived,
            request.DateAdmitted,
            resolvedClassAdmittedInto,
            request.AdmissionType,
            request.AdmissionTypeNote,
            request.AssessmentRequired,
            request.AssessmentResultRemarks,
            request.AssignedClassTeacher,
            request.DeclarationName,
            request.DeclarationSigned,
            request.DeclarationDate,
            request.HeadOfSchoolConfirmed,
            request.HeadOfSchoolName,
            today);

        if (update.IsFailure)
        {
            return Result.Failure<AdmissionRecordDto>(update.Error);
        }

        await auditSink.RecordAsync(
            Privileges.Pupil.Update,
            EntityType,
            record.PupilId.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        var classAdmittedIntoName = levelsById.TryGetValue(record.ClassAdmittedInto, out var finalLevel) ? finalLevel.Name : null;

        return Result.Success(AdmissionRecordMapper.ToDto(record, classAdmittedIntoName));
    }
}
