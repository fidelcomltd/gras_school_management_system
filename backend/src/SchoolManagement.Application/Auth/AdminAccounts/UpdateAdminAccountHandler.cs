using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>Handles <see cref="UpdateAdminAccountCommand"/>.</summary>
/// <remarks>
/// PRIVILEGE CHECK IS DATA-DEPENDENT (self vs. <c>admin.update</c>, spec 6.1.2), so this route is
/// mapped with <c>RequireAuthenticatedCaller()</c> rather than <c>RequirePrivilege(...)</c> — the
/// branching below IS the enforcement, mirroring how <c>ChangePasswordCommandHandler</c> is
/// inherently self-only. This is the one place <see cref="ICurrentUser.UserId"/> decides whether an
/// operation is permitted (its own doc comment warns against exactly that in general) — legitimate
/// here because spec 6.1.2's carve-out is defined in terms of "the account itself," which a
/// route-declarative privilege cannot express.
/// </remarks>
internal sealed class UpdateAdminAccountCommandHandler(
    IAdminAccountRepository accounts,
    IAdminSessionRepository sessions,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateAdminAccountCommand, Result<AdminAccountDetailDto>>
{
    private const string EntityType = "admin_account";

    /// <inheritdoc />
    public async Task<Result<AdminAccountDetailDto>> HandleAsync(
        UpdateAdminAccountCommand request,
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

        var isSelf = actorId == target.Id;

        var grants = await effectivePrivilegeProvider
            .GetGrantsAsync(actorId.ToString("D", CultureInfo.InvariantCulture), cancellationToken)
            .ConfigureAwait(false);

        var hasAdminUpdate = grants.Any(grant => grant.Privilege == Privileges.Admin.Update);

        // Rule 4's actor check is the acting admin's OWN flag, not merely a privilege — reuse `target`
        // when editing self rather than a second read.
        var actorIsSuperAdmin = isSelf
            ? target.IsSuperAdmin
            : (await accounts.FindReadOnlyByIdAsync(actorId, cancellationToken).ConfigureAwait(false))
                ?.IsSuperAdmin ?? false;

        var wantsSuperAdminChange = request.IsSuperAdmin is { } requestedIsSuperAdmin &&
            requestedIsSuperAdmin != target.IsSuperAdmin;

        if (wantsSuperAdminChange && !actorIsSuperAdmin)
        {
            // Spec 6.1.7 preamble: "all [four rules], producing an audit event on rejection so that
            // an attempt is visible even though it failed." RecordRejectionAsync (not RecordAsync):
            // this row must survive the ambient transaction's rollback below (TASK-0048).
            await auditSink.RecordRejectionAsync(
                "admin.super_admin_grant_denied",
                EntityType,
                target.Id.ToString("D", CultureInfo.InvariantCulture),
                metadata: null,
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<AdminAccountDetailDto>(Error.Forbidden(
                "admin.super_admin_grant_denied",
                "Only an existing Super Admin can grant or remove Super Admin status."));
        }

        if (!hasAdminUpdate)
        {
            if (!isSelf)
            {
                return Result.Failure<AdminAccountDetailDto>(Error.Forbidden(
                    "admin.access_denied",
                    "You do not have permission to edit this account."));
            }

            var emailUnchanged = string.Equals(
                request.Email.Trim(),
                target.Email,
                StringComparison.OrdinalIgnoreCase);

            if (!emailUnchanged || wantsSuperAdminChange)
            {
                return Result.Failure<AdminAccountDetailDto>(Error.Forbidden(
                    "admin.self_edit_restricted",
                    "Without admin.update you may only change your own name and phone."));
            }

            var selfEdit = target.ChangeOwnDetails(request.StaffName, request.Phone);

            if (selfEdit.IsFailure)
            {
                return Result.Failure<AdminAccountDetailDto>(selfEdit.Error);
            }
        }
        else
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var emailChanged = !string.Equals(normalizedEmail, target.Email, StringComparison.Ordinal);

            if (emailChanged &&
                await accounts.EmailExistsActiveOrSuspendedAsync(normalizedEmail, target.Id, cancellationToken)
                    .ConfigureAwait(false))
            {
                return Result.Failure<AdminAccountDetailDto>(Error.Conflict(
                    "admin.email_taken",
                    "An active or suspended account already uses this email."));
            }

            if (wantsSuperAdminChange && !request.IsSuperAdmin!.Value && target.Status == AdminAccountStatus.Active)
            {
                // Spec 4.1: clearing the flag on the currently only active Super Admin is exactly
                // "an operation that would leave zero active accounts with is_super_admin true" —
                // row-locked, not a pre-flight read, so a concurrent attempt on a DIFFERENT account
                // serialises against this one (spec 6.1.13).
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

            var update = target.UpdateDetails(request.StaffName, request.Email, request.Phone);

            if (update.IsFailure)
            {
                return Result.Failure<AdminAccountDetailDto>(update.Error);
            }

            if (wantsSuperAdminChange)
            {
                target.SetSuperAdmin(request.IsSuperAdmin!.Value);

                // Spec 9.1: "rotated on privilege... change" — the is_super_admin flag flip is the
                // first time a privilege change actually happens outside the acting account's own
                // session. There is no live connection to hand the target's browser a new raw token,
                // so rotating the stored hash has the same practical effect as revocation: the
                // browser's existing cookie stops matching any session row.
                var now = timeProvider.GetUtcNow();
                var activeSessions = await sessions
                    .FindActiveForAccountAsync(target.Id, now, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var session in activeSessions)
                {
                    session.RotateToken(SessionTokens.HashToken(SessionTokens.GenerateRawToken()));
                }
            }
        }

        await auditSink.RecordAsync(
            Privileges.Admin.Update,
            EntityType,
            target.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(AdminAccountMapper.ToDetailDto(target));
    }
}
