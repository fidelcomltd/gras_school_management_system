using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// Handles <see cref="UpdateSubjectCommand"/>. The route requires only <c>subject.update</c> —
/// changing <see cref="UpdateSubjectCommand.Status"/> ADDITIONALLY requires <c>subject.deactivate</c>,
/// checked here because it is data-dependent, the same shape <c>UpdateArmHandler</c> uses for
/// <c>arm.formteacher.assign</c>.
/// </summary>
internal sealed class UpdateSubjectHandler(
    ISubjectRepository subjects,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<UpdateSubjectCommand, Result<SubjectDto>>
{
    private const string EntityType = "subject";

    /// <inheritdoc />
    public async Task<Result<SubjectDto>> HandleAsync(UpdateSubjectCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subject = await subjects.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (subject is null)
        {
            return Result.Failure<SubjectDto>(Error.NotFound("subject.not_found", "No subject was found with that id."));
        }

        var newName = request.Name ?? subject.Name;
        var newCode = request.Code is null ? subject.Code : (request.Code.Length == 0 ? null : request.Code);
        var newDescription = request.Description is null ? subject.Description : (request.Description.Length == 0 ? null : request.Description);

        if (request.Name is not null)
        {
            var nameKey = request.Name.Trim().ToLowerInvariant();

            if (!string.Equals(nameKey, subject.NameKey, StringComparison.Ordinal) &&
                await subjects.NameExistsAsync(nameKey, subject.Id, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure<SubjectDto>(Error.Conflict(
                    "subject.name_duplicate", $"A subject named {request.Name.Trim()} already exists."));
            }
        }

        if (!string.IsNullOrEmpty(newCode))
        {
            var codeKey = newCode.Trim().ToLowerInvariant();

            if (!string.Equals(codeKey, subject.CodeKey, StringComparison.Ordinal))
            {
                var conflicting = await subjects.FindByCodeKeyAsync(codeKey, subject.Id, cancellationToken).ConfigureAwait(false);

                if (conflicting is not null)
                {
                    return Result.Failure<SubjectDto>(Error.Conflict(
                        "subject.code_duplicate", $"The code {newCode.Trim()} is already used by {conflicting.Name}."));
                }
            }
        }

        var edit = subject.Edit(newName, newCode, newDescription);

        if (edit.IsFailure)
        {
            return Result.Failure<SubjectDto>(edit.Error);
        }

        if (request.Status is { } status && status != subject.Status)
        {
            if (currentUser.UserId is not { } actorId)
            {
                return Result.Failure<SubjectDto>(Error.Unauthenticated(
                    "authentication.required", "Sign in to perform this action."));
            }

            var grants = await effectivePrivilegeProvider.GetGrantsAsync(actorId, cancellationToken).ConfigureAwait(false);

            if (!grants.Any(grant => grant.Privilege == Privileges.Subject.Deactivate))
            {
                return Result.Failure<SubjectDto>(Error.Forbidden(
                    "subject.deactivate_denied", "You do not have permission to change this subject's status."));
            }

            if (status == SubjectStatus.Active)
            {
                subject.Activate();
            }
            else
            {
                subject.Deactivate();
            }
        }

        await auditSink.RecordAsync(
            Privileges.Subject.Update,
            EntityType,
            subject.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = subject.Name,
                ["code"] = subject.Code,
                ["status"] = subject.Status.ToString(),
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SubjectMapper.ToDto(subject, mappedLevelCount: null, armExceptionCount: null, pupilsTakingCount: null));
    }
}
