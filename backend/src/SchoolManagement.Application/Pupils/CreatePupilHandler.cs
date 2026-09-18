using System.Globalization;
using SchoolManagement.Application.Abstractions.Admissions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Admissions;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils;

/// <summary>Handles <see cref="CreatePupilCommand"/>.</summary>
internal sealed class CreatePupilHandler(
    IPupilRepository pupils,
    IAdmissionRecordRepository admissionRecords,
    IClassLevelRepository classLevels,
    IAcademicSessionRepository sessions,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<CreatePupilCommand, Result<PupilDto>>
{
    private const string EntityType = "pupil";

    /// <inheritdoc />
    public async Task<Result<PupilDto>> HandleAsync(CreatePupilCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // Section A's own references (spec 6.5.9) are resolved BEFORE the pupil is created, so a bad
        // session or class level fails the whole request without leaving a pupil row behind — the
        // handler still returns a single Result, and UnitOfWorkBehavior rolls back either way, but
        // resolving first avoids constructing a pupil the caller will never see.
        var classLevel = (await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(level => level.Id.ToString("D", CultureInfo.InvariantCulture) == request.Admission.ClassAdmittedInto);

        if (classLevel is null || classLevel.Status != LevelStatus.Active)
        {
            return Result.Failure<PupilDto>(Error.Validation(
                "admission_record.class_admitted_into_invalid", "ClassAdmittedInto must reference an existing, active class level."));
        }

        Guid sessionId;

        if (request.Admission.SessionId is not null)
        {
            sessionId = Guid.Parse(request.Admission.SessionId);
            var session = await sessions.FindReadOnlyByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

            if (session is null)
            {
                return Result.Failure<PupilDto>(Error.Validation(
                    "admission_record.session_id_invalid", "SessionId must reference an existing session."));
            }
        }
        else
        {
            var activeSession = await sessions.FindActiveAsync(cancellationToken).ConfigureAwait(false);

            if (activeSession is null)
            {
                return Result.Failure<PupilDto>(Error.Conflict(
                    "admission_record.no_active_session",
                    "SessionId was not supplied and there is no active session to default to. Open a session first."));
            }

            sessionId = activeSession.Id;
        }

        var creation = Pupil.Create(
            Guid.CreateVersion7(),
            request.Surname,
            request.FirstName,
            request.MiddleName,
            request.Sex,
            request.DateOfBirth,
            today,
            request.Nationality,
            request.StateOfOrigin,
            request.Lga,
            request.HomeAddress,
            request.PreviousSchool,
            request.PreviousClass,
            request.OtherInformation);

        if (creation.IsFailure)
        {
            return Result.Failure<PupilDto>(creation.Error);
        }

        var newPupil = creation.Value;

        var admissionCreation = AdmissionRecord.Create(
            Guid.CreateVersion7(),
            newPupil.Id,
            sessionId,
            request.Admission.DateApplicationReceived,
            request.Admission.DateAdmitted,
            today,
            classLevel.Id,
            request.Admission.AdmissionType,
            request.Admission.AdmissionTypeNote,
            request.Admission.AssessmentRequired);

        if (admissionCreation.IsFailure)
        {
            return Result.Failure<PupilDto>(admissionCreation.Error);
        }

        var newAdmissionRecord = admissionCreation.Value;

        // Created in the SAME transaction as the pupil (TASK-0062's own acceptance criterion) —
        // UnitOfWorkBehavior commits both AddAsync calls together, so a pupil with no admission
        // record is a state this handler cannot produce.
        await pupils.AddAsync(newPupil, cancellationToken).ConfigureAwait(false);
        await admissionRecords.AddAsync(newAdmissionRecord, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Pupil.Create,
            EntityType,
            newPupil.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        var admissionDto = AdmissionRecordMapper.ToDto(newAdmissionRecord, classLevel.Name);

        return Result.Success(PupilMapper.ToDto(newPupil, today, admission: admissionDto));
    }
}
