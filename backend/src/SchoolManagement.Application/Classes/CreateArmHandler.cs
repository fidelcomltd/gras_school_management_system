using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Classes;

/// <summary>Handles <see cref="CreateArmCommand"/>.</summary>
internal sealed class CreateArmHandler(
    IArmRepository arms,
    IClassLevelRepository levels,
    IAcademicSessionRepository sessions,
    IAdminAccountRepository adminAccounts,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<CreateArmCommand, Result<ArmDto>>
{
    private const string EntityType = "arm";

    /// <inheritdoc />
    public async Task<Result<ArmDto>> HandleAsync(CreateArmCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var classLevelId = Guid.Parse(request.ClassLevelId);
        var sessionId = Guid.Parse(request.SessionId);

        var allLevels = await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var level = allLevels.FirstOrDefault(candidate => candidate.Id == classLevelId);

        if (level is null || level.Status != LevelStatus.Active)
        {
            return Result.Failure<ArmDto>(Error.Validation(
                "arm.level_not_found", "No active class level was found with that id."));
        }

        var session = await sessions.FindReadOnlyByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (session is null)
        {
            return Result.Failure<ArmDto>(Error.Validation("arm.session_not_found", "No session was found with that id."));
        }

        if (session.State == SessionState.Closed)
        {
            return Result.Failure<ArmDto>(Error.Conflict(
                "arm.session_closed", $"{session.Name} is closed. Arms can only be created in an upcoming or active session."));
        }

        var labelKey = request.Label.Trim().ToLowerInvariant();

        if (await arms.LabelExistsAsync(classLevelId, sessionId, labelKey, excludingId: null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ArmDto>(Error.Conflict(
                "arm.label_duplicate", $"{level.Name} already has an arm labelled {request.Label.Trim()} in {session.Name}."));
        }

        Guid? formTeacherAdminId = null;

        if (request.FormTeacherAdminId is not null)
        {
            formTeacherAdminId = Guid.Parse(request.FormTeacherAdminId);
            var formTeacherCheck = await ValidateActiveAdminAsync(formTeacherAdminId.Value, adminAccounts, cancellationToken).ConfigureAwait(false);

            if (formTeacherCheck.IsFailure)
            {
                return Result.Failure<ArmDto>(formTeacherCheck.Error);
            }
        }

        var creation = Arm.Create(
            Guid.CreateVersion7(), classLevelId, sessionId, request.Label, request.Capacity, formTeacherAdminId);

        if (creation.IsFailure)
        {
            return Result.Failure<ArmDto>(creation.Error);
        }

        var newArm = creation.Value;
        await arms.AddAsync(newArm, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Arm.Create,
            EntityType,
            newArm.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        var levelNamesById = allLevels.ToDictionary(candidate => candidate.Id, candidate => candidate.Name);
        return Result.Success(ArmMapper.ToDto(newArm, levelNamesById));
    }

    /// <summary>Spec 6.4.3: a form teacher "must reference an active admin account." Shared by create and update.</summary>
    internal static async Task<Result> ValidateActiveAdminAsync(
        Guid adminId, IAdminAccountRepository adminAccounts, CancellationToken cancellationToken)
    {
        var account = await adminAccounts.FindReadOnlyByIdAsync(adminId, cancellationToken).ConfigureAwait(false);

        return account is { Status: AdminAccountStatus.Active }
            ? Result.Success()
            : Result.Failure(Error.Validation(
                "arm.form_teacher_not_found", "No active admin account was found with that id."));
    }
}
