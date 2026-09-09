using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Assignments;

/// <summary>Handles <see cref="RevokeRoleAssignmentCommand"/>.</summary>
internal sealed class RevokeRoleAssignmentCommandHandler(
    IRoleAssignmentRepository assignments,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<RevokeRoleAssignmentCommand, Result>
{
    private const string EntityType = "role_assignment";

    /// <inheritdoc />
    public async Task<Result> HandleAsync(RevokeRoleAssignmentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } actorIdText || !Guid.TryParse(actorIdText, out var actorId))
        {
            return Result.Failure(Error.Unauthenticated(
                "authentication.required", "Sign in to perform this action."));
        }

        var id = Guid.Parse(request.Id);
        var assignment = await assignments.FindTrackedByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (assignment is null)
        {
            return Result.Failure(Error.NotFound(
                "role_assignment.not_found", "No assignment was found with that id."));
        }

        // Rule 1 (spec 6.1.7) applies to revoke as well as create.
        var selfAssignment = RolePrivilegeEscalationGuard.ValidateNotSelfAssignment(
            actorId, assignment.AdminAccountId);

        if (selfAssignment.IsFailure)
        {
            await auditSink.RecordAsync(
                selfAssignment.Error.Code,
                EntityType,
                assignment.Id.ToString("D", CultureInfo.InvariantCulture),
                metadata: null,
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure(selfAssignment.Error);
        }

        assignment.Revoke();

        await auditSink.RecordAsync(
            Privileges.Role.Assign,
            EntityType,
            assignment.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
