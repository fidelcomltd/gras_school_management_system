using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>Handles <see cref="ChangeAdminAccountStatusCommand"/>.</summary>
/// <remarks>
/// PRIVILEGE CHECK IS DATA-DEPENDENT on the requested transition (spec 6.1.10), so this route is
/// mapped with <c>RequireAuthenticatedCaller()</c> rather than <c>RequirePrivilege(...)</c> — the
/// resolution below IS the enforcement.
/// </remarks>
internal sealed class ChangeAdminAccountStatusCommandHandler(
    IAdminAccountRepository accounts,
    IAdminSessionRepository sessions,
    IRoleAssignmentRepository assignments,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<ChangeAdminAccountStatusCommand, Result<AdminAccountDetailDto>>
{
    private const string EntityType = "admin_account";

    /// <inheritdoc />
    public async Task<Result<AdminAccountDetailDto>> HandleAsync(
        ChangeAdminAccountStatusCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } actorIdText || !Guid.TryParse(actorIdText, out var actorId))
        {
            return Result.Failure<AdminAccountDetailDto>(Error.Unauthenticated(
                "authentication.required",
                "Sign in to perform this action."));
        }

        var target = await accounts.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (target is null)
        {
            return Result.Failure<AdminAccountDetailDto>(Error.NotFound(
                "admin.not_found",
                "No admin account was found with that id."));
        }

        // B5 (approved delta): blocking self-status-change is an ADDED rule, not a spec derivation —
        // recorded in backend/docs/ASSUMPTIONS.md §2.16. Checked before privilege resolution: it is
        // an absolute block, independent of what the caller holds.
        if (actorId == target.Id)
        {
            return Result.Failure<AdminAccountDetailDto>(Error.Forbidden(
                "admin.self_status_change_forbidden",
                "You cannot change your own account's status. Ask another administrator."));
        }

        string requiredPrivilege;
        var requiresActorSuperAdmin = false;

        switch ((target.Status, request.Status))
        {
            case (AdminAccountStatus.Active, AdminAccountStatus.Suspended):
            case (AdminAccountStatus.Suspended, AdminAccountStatus.Active):
                requiredPrivilege = Privileges.Admin.Suspend;
                break;

            case (AdminAccountStatus.Active, AdminAccountStatus.Deactivated):
            case (AdminAccountStatus.Suspended, AdminAccountStatus.Deactivated):
                requiredPrivilege = Privileges.Admin.Deactivate;
                break;

            case (AdminAccountStatus.Deactivated, AdminAccountStatus.Active):
                // Spec 6.1.10: "Moves to active by admin.deactivate held by a Super Admin."
                requiredPrivilege = Privileges.Admin.Deactivate;
                requiresActorSuperAdmin = true;
                break;

            default:
                return Result.Failure<AdminAccountDetailDto>(Error.Validation(
                    "admin.invalid_status_transition",
                    $"An account cannot move from {target.Status} to {request.Status}."));
        }

        var grants = await effectivePrivilegeProvider
            .GetGrantsAsync(actorId.ToString("D", CultureInfo.InvariantCulture), cancellationToken)
            .ConfigureAwait(false);

        if (!grants.Any(grant => grant.Privilege == requiredPrivilege))
        {
            return Result.Failure<AdminAccountDetailDto>(Error.Forbidden(
                "admin.access_denied",
                "You do not have permission to change this account's status."));
        }

        if (requiresActorSuperAdmin)
        {
            var actorAccount = await accounts.FindReadOnlyByIdAsync(actorId, cancellationToken).ConfigureAwait(false);

            if (actorAccount is not { IsSuperAdmin: true })
            {
                return Result.Failure<AdminAccountDetailDto>(Error.Forbidden(
                    "admin.reactivation_requires_super_admin",
                    "Only a Super Admin may reactivate a deactivated account."));
            }
        }

        // Spec 4.1: moving a currently-active Super Admin out of Active (suspend or deactivate) must
        // not leave zero active Super Admins — row-locked, not a pre-flight read (spec 6.1.13).
        if (target.IsSuperAdmin && target.Status == AdminAccountStatus.Active)
        {
            var lockedActiveSuperAdminIds = await accounts
                .LockActiveSuperAdminIdsAsync(cancellationToken)
                .ConfigureAwait(false);

            if (lockedActiveSuperAdminIds.Contains(target.Id) && lockedActiveSuperAdminIds.Count <= 1)
            {
                return Result.Failure<AdminAccountDetailDto>(Error.Conflict(
                    "admin.last_active_super_admin",
                    "This is the only active Super Admin. Create and activate another Super Admin " +
                    "before changing this account."));
            }
        }

        var transition = target.ChangeStatus(request.Status);

        if (transition.IsFailure)
        {
            return Result.Failure<AdminAccountDetailDto>(transition.Error);
        }

        // Spec 6.1.10: suspension and deactivation revoke existing sessions immediately;
        // reactivation touches no session.
        if (request.Status is AdminAccountStatus.Suspended or AdminAccountStatus.Deactivated)
        {
            var now = timeProvider.GetUtcNow();
            var reason = request.Status == AdminAccountStatus.Suspended
                ? AdminSessionRevocationReasons.AccountSuspended
                : AdminSessionRevocationReasons.AccountDeactivated;

            var activeSessions = await sessions
                .FindActiveForAccountAsync(target.Id, now, cancellationToken)
                .ConfigureAwait(false);

            foreach (var session in activeSessions)
            {
                session.Revoke(now, reason);
            }
        }

        // Spec 6.1.10: "All active assignments are revoked as part of the transition" — deactivation
        // only. Reactivation (the Deactivated -> Active branch above) deliberately does NOT restore
        // them: "they are reassigned deliberately" (spec 6.1.10) — the seam TASK-0027 left, closed here.
        if (request.Status == AdminAccountStatus.Deactivated)
        {
            var activeAssignments = await assignments
                .ListActiveForAccountTrackedAsync(target.Id, cancellationToken)
                .ConfigureAwait(false);

            foreach (var assignment in activeAssignments)
            {
                assignment.Revoke();
            }
        }

        await auditSink.RecordAsync(
            requiredPrivilege,
            EntityType,
            target.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(AdminAccountMapper.ToDetailDto(target));
    }
}
