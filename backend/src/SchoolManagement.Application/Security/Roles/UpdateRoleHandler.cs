using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Roles;

/// <summary>Handles <see cref="UpdateRoleCommand"/>.</summary>
internal sealed class UpdateRoleCommandHandler(
    IRoleRepository roles,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<UpdateRoleCommand, Result<RoleDto>>
{
    private const string EntityType = "role";

    /// <summary>Spec 6.1.4: "A system role cannot be edited, renamed, deleted or have privileges removed."</summary>
    private static readonly Error SystemImmutableError = Error.Conflict(
        "role.system_immutable",
        "A system role cannot be edited, renamed, deleted or have privileges removed.");

    /// <inheritdoc />
    public async Task<Result<RoleDto>> HandleAsync(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } actorId)
        {
            return Result.Failure<RoleDto>(Error.Unauthenticated(
                "authentication.required",
                "Sign in to perform this action."));
        }

        var role = await roles.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (role is null)
        {
            return Result.Failure<RoleDto>(Error.NotFound("role.not_found", "No role was found with that id."));
        }

        // Checked before anything else, unconditionally: a system role rejects the WHOLE request with
        // 409, regardless of which fields it touches (spec 6.1.4, 6.1.14).
        if (role.IsSystem)
        {
            return Result.Failure<RoleDto>(SystemImmutableError);
        }

        if (request.Name is not null || request.Description is not null)
        {
            var effectiveName = request.Name ?? role.Name;
            var effectiveDescription = request.Description ?? role.Description;
            var newNameKey = effectiveName.Trim().ToLowerInvariant();

            if (!string.Equals(newNameKey, role.NameKey, StringComparison.Ordinal) &&
                await roles.NameExistsAsync(newNameKey, role.Id, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure<RoleDto>(Error.Conflict(
                    "role.name_duplicate",
                    "A role with that name already exists."));
            }

            var rename = role.Rename(effectiveName, effectiveDescription);

            if (rename.IsFailure)
            {
                return Result.Failure<RoleDto>(rename.Error);
            }
        }

        if (request.Privileges is not null)
        {
            // Snapshot BEFORE mutating: Role.Privileges is a live view over the same backing list, so
            // capturing it after SetPrivileges would show the NEW set on both sides of the escalation
            // check and never flag anything as an addition.
            var existingPrivileges = role.Privileges.ToArray();

            var setResult = role.SetPrivileges(request.Privileges);

            if (setResult.IsFailure)
            {
                return Result.Failure<RoleDto>(setResult.Error);
            }

            var grants = await effectivePrivilegeProvider.GetGrantsAsync(actorId, cancellationToken)
                .ConfigureAwait(false);
            var actorPrivileges = grants.Select(grant => grant.Privilege).ToArray();

            var escalation = RolePrivilegeEscalationGuard.ValidateAddition(
                existingPrivileges,
                role.Privileges,
                actorPrivileges);

            if (escalation.IsFailure)
            {
                // Spec 6.1.7 preamble: an audit event on rejection, even though the whole request still
                // fails and the SetPrivileges mutation above is rolled back with everything else.
                await auditSink.RecordAsync(
                    "role.privilege_escalation",
                    EntityType,
                    role.Id.ToString("D", CultureInfo.InvariantCulture),
                    metadata: null,
                    actorAdminId: currentUser.UserId,
                    cancellationToken).ConfigureAwait(false);

                return Result.Failure<RoleDto>(escalation.Error);
            }
        }

        if (request.Status is { } status)
        {
            role.ChangeStatus(status);
        }

        await auditSink.RecordAsync(
            Privileges.Role.Update,
            EntityType,
            role.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(RoleMapper.ToDto(role));
    }
}
