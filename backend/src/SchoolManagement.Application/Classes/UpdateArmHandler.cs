using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// Handles <see cref="UpdateArmCommand"/>. The route requires only <c>arm.update</c> — setting
/// <see cref="UpdateArmCommand.FormTeacherAdminId"/> ADDITIONALLY requires
/// <c>arm.formteacher.assign</c>, checked here because it is data-dependent, the same shape
/// <c>UpdateLevelHandler</c> uses for its own data-dependent <c>level.deactivate</c> check.
/// </summary>
internal sealed class UpdateArmHandler(
    IArmRepository arms,
    IClassLevelRepository levels,
    IAdminAccountRepository adminAccounts,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<UpdateArmCommand, Result<ArmDto>>
{
    private const string EntityType = "arm";

    /// <inheritdoc />
    public async Task<Result<ArmDto>> HandleAsync(UpdateArmCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var arm = await arms.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (arm is null)
        {
            return Result.Failure<ArmDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var allLevels = await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var levelNamesById = allLevels.ToDictionary(level => level.Id, level => level.Name);
        var levelName = levelNamesById.TryGetValue(arm.ClassLevelId, out var name) ? name : "Unknown level";

        // Spec 6.4.7: "The arm becomes read-only... no form teacher change" once closed — checked
        // before applying ANY of the fields below, not per-field.
        var mutableCheck = arm.EnsureMutable(ArmDisplayName.Compose(levelName, arm.Label));

        if (mutableCheck.IsFailure)
        {
            return Result.Failure<ArmDto>(mutableCheck.Error);
        }

        if (request.Label is not null)
        {
            var labelKey = request.Label.Trim().ToLowerInvariant();

            if (!string.Equals(labelKey, arm.LabelKey, StringComparison.Ordinal) &&
                await arms.LabelExistsAsync(arm.ClassLevelId, arm.SessionId, labelKey, arm.Id, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure<ArmDto>(Error.Conflict(
                    "arm.label_duplicate", $"{levelName} already has an arm labelled {request.Label.Trim()} in this session."));
            }

            var relabel = arm.ChangeLabel(request.Label);

            if (relabel.IsFailure)
            {
                return Result.Failure<ArmDto>(relabel.Error);
            }
        }

        if (request.Capacity is { } capacity)
        {
            var capacityChange = arm.ChangeCapacity(capacity);

            if (capacityChange.IsFailure)
            {
                return Result.Failure<ArmDto>(capacityChange.Error);
            }
        }

        if (request.FormTeacherAdminId is not null)
        {
            if (currentUser.UserId is not { } actorId)
            {
                return Result.Failure<ArmDto>(Error.Unauthenticated(
                    "authentication.required", "Sign in to perform this action."));
            }

            var grants = await effectivePrivilegeProvider.GetGrantsAsync(actorId, cancellationToken).ConfigureAwait(false);

            if (!grants.Any(grant => grant.Privilege == Privileges.Arm.FormTeacherAssign))
            {
                return Result.Failure<ArmDto>(Error.Forbidden(
                    "arm.formteacher_assign_denied", "You do not have permission to assign a form teacher."));
            }

            if (request.FormTeacherAdminId.Length == 0)
            {
                arm.AssignFormTeacher(null);
            }
            else
            {
                var formTeacherId = Guid.Parse(request.FormTeacherAdminId);
                var formTeacherCheck = await CreateArmHandler
                    .ValidateActiveAdminAsync(formTeacherId, adminAccounts, cancellationToken)
                    .ConfigureAwait(false);

                if (formTeacherCheck.IsFailure)
                {
                    return Result.Failure<ArmDto>(formTeacherCheck.Error);
                }

                arm.AssignFormTeacher(formTeacherId);
            }
        }

        if (request.Status is { } status && status != arm.Status)
        {
            if (status == ArmStatus.Active)
            {
                arm.Activate();
            }
            else
            {
                arm.Deactivate();
            }
        }

        await auditSink.RecordAsync(
            Privileges.Arm.Update,
            EntityType,
            arm.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(ArmMapper.ToDto(arm, levelNamesById));
    }
}
