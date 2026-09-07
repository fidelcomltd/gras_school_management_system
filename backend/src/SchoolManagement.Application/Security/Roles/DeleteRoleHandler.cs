using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Roles;

/// <summary>Handles <see cref="DeleteRoleCommand"/>.</summary>
internal sealed class DeleteRoleCommandHandler(
    IRoleRepository roles,
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
