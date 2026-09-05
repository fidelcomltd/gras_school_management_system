using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.ChangePassword;

/// <summary>Handles <see cref="ChangePasswordCommand"/>.</summary>
internal sealed class ChangePasswordCommandHandler(
    IAdminAccountRepository accounts,
    IAdminSessionRepository sessions,
    IPasswordHasher passwordHasher,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ICurrentSession currentSession,
    TimeProvider timeProvider)
    : IRequestHandler<ChangePasswordCommand, Result<ChangePasswordResult>>
{
    /// <inheritdoc />
    public async Task<Result<ChangePasswordResult>> HandleAsync(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The authentication/authorization pipeline guarantees an authenticated caller with a valid
        // session reaches here (RequireAuthenticatedCaller + the must-change-password gate's own
        // exemption for this endpoint) — both are non-null in practice. Failing closed rather than
        // trusting that is still the honest response if either is somehow absent.
        if (currentUser.UserId is not { } userIdText ||
            !Guid.TryParse(userIdText, out var accountId) ||
            currentSession.SessionId is not { } sessionId)
        {
            return Result.Failure<ChangePasswordResult>(Error.Unauthenticated(
                "authentication.required",
                "Sign in to perform this action."));
        }

        var account = await accounts.FindTrackedByIdAsync(accountId, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            return Result.Failure<ChangePasswordResult>(Error.Unauthenticated(
                "authentication.required",
                "Sign in to perform this action."));
        }

        // Unconditional, matching sign-in's timing discipline — this is a re-authentication check for
        // a sensitive action, not an RBAC matter (human ruling, approved contract delta §2/§3).
        if (!passwordHasher.Verify(request.CurrentPassword, account.PasswordHash))
        {
            return Result.Failure<ChangePasswordResult>(Error.Unauthenticated(
                "auth.current_password_incorrect",
                "Your current password is not correct."));
        }

        // Verify the CANDIDATE PLAINTEXT against every retained hash — comparing encoded hash strings
        // would never catch a reuse, since Argon2id salts each hash independently (see the entity's
        // remarks on AllPasswordHashesForReuseCheck).
        var isReused = account.AllPasswordHashesForReuseCheck()
            .Any(existingHash => passwordHasher.Verify(request.NewPassword, existingHash));

        if (isReused)
        {
            return Result.Failure<ChangePasswordResult>(new ValidationError(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(ChangePasswordCommand.NewPassword)] =
                        ["This password has been used recently. Choose a different one."],
                }));
        }

        var now = timeProvider.GetUtcNow();

        account.ChangePassword(passwordHasher.Hash(request.NewPassword));

        // Spec 6.1.11: revokes every OTHER active session for the account, not this one.
        var otherSessions = await sessions
            .FindTrackedActiveForAccountExceptAsync(accountId, sessionId, now, cancellationToken)
            .ConfigureAwait(false);

        foreach (var otherSession in otherSessions)
        {
            otherSession.Revoke(now, AdminSessionRevocationReasons.PasswordChanged);
        }

        // Spec 9.1: the session token itself rotates on a password change. Same session row, new
        // token — the current session survives (it is the one performing the change).
        var currentSessionRow = await sessions.FindTrackedByIdAsync(sessionId, cancellationToken)
            .ConfigureAwait(false);

        if (currentSessionRow is null)
        {
            // The session validated the request moments ago; it cannot have vanished mid-request
            // under this application's single-writer-per-request model. Treated as an authentication
            // failure rather than an assertion, since a Result is how this layer reports "no".
            return Result.Failure<ChangePasswordResult>(Error.Unauthenticated(
                "authentication.session_revoked",
                "Your session is no longer valid."));
        }

        var rawToken = SessionTokens.GenerateRawToken();
        currentSessionRow.RotateToken(SessionTokens.HashToken(rawToken));

        var grants = await effectivePrivilegeProvider
            .GetGrantsAsync(account.Id.ToString(), cancellationToken)
            .ConfigureAwait(false);

        var response = AuthSessionResponse.Create(
            account.Id,
            account.Email,
            account.StaffName,
            account.IsSuperAdmin,
            account.MustChangePassword,
            grants,
            currentSessionRow.IdleExpiresAtUtc,
            currentSessionRow.AbsoluteExpiresAtUtc);

        return Result.Success(new ChangePasswordResult(response, rawToken));
    }
}
