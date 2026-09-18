using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Roles;

/// <summary>Handles <see cref="DeleteRoleCommand"/>.</summary>
/// <remarks>
/// Spec 9.4: hard delete is permitted only "when no assignment has ever used it"; otherwise the role
/// is archived instead. TASK-0028 shipped the unconditional hard-delete branch because no
/// <c>role_assignment</c> table existed yet (<c>IRoleRepository.RemoveAsync</c>'s own remarks and the
/// STATE.md live-drift entry it left behind); this is the has-ever-been-assigned branch TASK-0030
/// promised to add in the same dispatch that created the table.
/// </remarks>
internal sealed class DeleteRoleCommandHandler(
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<DeleteRoleCommand, Result>
{
    private const string EntityType = "role";

    /// <inheritdoc />
    public async Task<Result> HandleAsync(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var role = await roles.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (role is null)
        {
            return Result.Failure(Error.NotFound("role.not_found", "No role was found with that id."));
        }

        if (role.IsSystem)
        {
            return Result.Failure(Error.Conflict(
                "role.system_immutable",
                "A system role cannot be edited, renamed, deleted or have privileges removed."));
        }

        var everAssigned = await assignments.ExistsForRoleAsync(role.Id, cancellationToken).ConfigureAwait(false);

        if (everAssigned)
        {
            role.ChangeStatus(RoleStatus.Archived);

            await auditSink.RecordAsync(
                Privileges.Role.Delete,
                EntityType,
                role.Id.ToString("D", CultureInfo.InvariantCulture),
                metadata: null,
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }

        await roles.RemoveAsync(role, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Role.Delete,
            EntityType,
            role.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
