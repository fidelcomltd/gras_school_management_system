using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Assignments;

/// <summary>Handles <see cref="CreateRoleAssignmentCommand"/>.</summary>
/// <remarks>
/// PRIVILEGE CHECK IS DATA-DEPENDENT on <see cref="CreateRoleAssignmentCommand.ScopeType"/> (spec
/// 6.1.5's contract table: <c>role.assign</c> for school-wide, <c>role.scope.assign</c> for arm-list),
/// so this route is mapped with <c>RequireAuthenticatedCaller()</c> rather than
/// <c>RequirePrivilege(...)</c> — the resolution below IS the enforcement, same shape as
/// <c>ChangeAdminAccountStatusCommandHandler</c>.
/// </remarks>
internal sealed class CreateRoleAssignmentCommandHandler(
    IRoleAssignmentRepository assignments,
    IAdminAccountRepository accounts,
    IRoleRepository roles,
    IAcademicSessionRepository sessions,
    IArmRepository arms,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<CreateRoleAssignmentCommand, Result<RoleAssignmentDto>>
{
    private const string EntityType = "role_assignment";

    /// <inheritdoc />
    public async Task<Result<RoleAssignmentDto>> HandleAsync(
        CreateRoleAssignmentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } actorIdText || !Guid.TryParse(actorIdText, out var actorId))
        {
            return Result.Failure<RoleAssignmentDto>(Error.Unauthenticated(
                "authentication.required", "Sign in to perform this action."));
        }

        var targetAccountId = Guid.Parse(request.AdminAccountId);

        var targetAccount = await accounts.FindReadOnlyByIdAsync(targetAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (targetAccount is null)
        {
            return Result.Failure<RoleAssignmentDto>(Error.NotFound(
                "admin.not_found", "No admin account was found with that id."));
        }

        // Rule 1 (spec 6.1.7): an absolute block, checked before anything else the actor holds —
        // same ordering ChangeAdminAccountStatusCommandHandler uses for its own added self-block.
        var selfAssignment = RolePrivilegeEscalationGuard.ValidateNotSelfAssignment(actorId, targetAccountId);

        if (selfAssignment.IsFailure)
        {
            await auditSink.RecordAsync(
                selfAssignment.Error.Code,
                EntityType,
                entityId: null,
                metadata: null,
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<RoleAssignmentDto>(selfAssignment.Error);
        }

        if (targetAccount.Status == AdminAccountStatus.Deactivated)
        {
            return Result.Failure<RoleAssignmentDto>(Error.Conflict(
                "role_assignment.target_deactivated",
                "A deactivated account cannot receive a role assignment."));
        }

        var roleId = Guid.Parse(request.RoleId);
        var role = await roles.FindReadOnlyByIdAsync(roleId, cancellationToken).ConfigureAwait(false);

        if (role is null)
        {
            return Result.Failure<RoleAssignmentDto>(Error.NotFound(
                "role.not_found", "No role was found with that id."));
        }

        // is_super_admin is a flag bypass, never a role assignment (human ruling 2026-09-05: spec
        // 6.1.7 rule 4 governs over 4.2.2's looser wording — see AdminAccount's remarks). Rejecting
        // this here keeps that ruling the ONLY path to full privileges, rather than opening a second,
        // unaudited one through the role_assignment table this card builds.
        if (role.Id == SeededRoles.SuperAdminId)
        {
            return Result.Failure<RoleAssignmentDto>(Error.Validation(
                "role_assignment.super_admin_not_assignable",
                "The Super Admin role is granted through the is_super_admin flag, not through a " +
                "role assignment."));
        }

        var sessionId = Guid.Parse(request.SessionId);
        var session = await sessions.FindReadOnlyByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (session is null)
        {
            return Result.Failure<RoleAssignmentDto>(Error.NotFound(
                "session.not_found", "No session was found with that id."));
        }

        var requestedArmIds = (request.ArmIds ?? [])
            .Select(Guid.Parse)
            .ToArray();

        if (request.ScopeType == ScopeType.ArmList)
        {
            var allArms = await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
            var armsById = allArms.ToDictionary(arm => arm.Id);

            var notInSession = requestedArmIds
                .Where(armId => !armsById.TryGetValue(armId, out var arm) || arm.SessionId != sessionId)
                .Select(armId => armId.ToString("D", CultureInfo.InvariantCulture))
                .ToArray();

            if (notInSession.Length > 0)
            {
                return Result.Failure<RoleAssignmentDto>(Error.Validation(
                    "role_assignment.arm_not_in_session",
                    $"These arms do not belong to the named session: {string.Join(", ", notInSession)}."));
            }
        }

        // Spec 4.2: an arm-scoped assignment is rejected outright when the role holds any
        // non-scopable privilege.
        var assignable = RoleScopeGuard.ValidateAssignable(role.Privileges, request.ScopeType);

        if (assignable.IsFailure)
        {
            return Result.Failure<RoleAssignmentDto>(assignable.Error);
        }

        var actorGrants = await effectivePrivilegeProvider
            .GetGrantsAsync(actorIdText, cancellationToken)
            .ConfigureAwait(false);

        var assigningPrivilege = request.ScopeType == ScopeType.SchoolWide
            ? Privileges.Role.Assign
            : Privileges.Role.ScopeAssign;

        var matchingGrants = actorGrants
            .Where(grant => string.Equals(grant.Privilege, assigningPrivilege, StringComparison.Ordinal))
            .ToArray();

        if (matchingGrants.Length == 0)
        {
            return Result.Failure<RoleAssignmentDto>(Error.Forbidden(
                "role_assignment.access_denied",
                "You do not have permission to create this assignment."));
        }

        var actorScopeIsSchoolWide = matchingGrants.Any(grant => grant.Scope == ScopeType.SchoolWide);
        var actorArmIds = new HashSet<Guid>(matchingGrants
            .Where(grant => grant.Scope == ScopeType.ArmList)
            .SelectMany(grant => grant.ArmIds));

        // Rule 3 (spec 6.1.7): no account may grant a scope wider than its own.
        var withinScope = RoleScopeGuard.ValidateGrantWithinActorScope(
            actorScopeIsSchoolWide, actorArmIds, request.ScopeType, requestedArmIds);

        if (withinScope.IsFailure)
        {
            await auditSink.RecordAsync(
                withinScope.Error.Code,
                EntityType,
                entityId: null,
                metadata: null,
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<RoleAssignmentDto>(withinScope.Error);
        }

        var creation = RoleAssignment.Create(
            Guid.CreateVersion7(),
            targetAccountId,
            roleId,
            sessionId,
            request.ScopeType,
            requestedArmIds,
            actorId);

        if (creation.IsFailure)
        {
            return Result.Failure<RoleAssignmentDto>(creation.Error);
        }

        var assignment = creation.Value;

        await assignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            assigningPrivilege,
            EntityType,
            assignment.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(RoleAssignmentMapper.ToDto(assignment));
    }
}
