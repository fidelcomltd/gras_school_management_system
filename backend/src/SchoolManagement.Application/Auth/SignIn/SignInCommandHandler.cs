using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.SignIn;

/// <summary>Handles <see cref="SignInCommand"/>.</summary>
/// <remarks>
/// <para>
/// TIMING: <see cref="IPasswordHasher.Verify"/> runs EXACTLY ONCE, unconditionally, before any branch
/// on whether the account exists or is locked — approved contract delta §2's binding ruling. Skipping
/// it for a nonexistent account or a locked one (the "obvious" optimisation) would turn response
/// timing into the very enumeration oracle spec 6.1.11's identical-message rule exists to close.
/// </para>
/// <para>
/// LOCKOUT PERSISTENCE: a failed attempt's counter update must survive even though this handler then
/// returns a FAILED <see cref="Result"/>, which <c>UnitOfWork.ExecuteAtomicallyAsync</c> would
/// otherwise roll back. See <see cref="IAdminAccountRepository.PersistLockoutStateAsync"/>'s remarks
/// for why that call — and only that call — bypasses the normal commit-on-success rule.
/// </para>
/// </remarks>
// TODO(TASK-0019): sign-in, sign-in failure and lockout are not yet written to audit_event. Spec
// 6.1.12's recorded-actions list never names them explicitly, though a lockout write touches
// admin_account, a governed entity, by the general rule — orchestrator's open question 4, deliberately
// left open rather than silently dropped. Wire this once the audit transaction TASK-0002 seamed
// (IAuthorizationAuditSink today only records privilege rejections) is populated for real.
internal sealed class SignInCommandHandler(
    IAdminAccountRepository accounts,
    IAdminSessionRepository sessions,
    IPasswordHasher passwordHasher,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    TimeProvider timeProvider)
    : IRequestHandler<SignInCommand, Result<SignInResult>>
{
    /// <inheritdoc />
    public async Task<Result<SignInResult>> HandleAsync(SignInCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var account = await accounts.FindTrackedByEmailAsync(normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        // Unconditional — see the class remarks. Never short-circuit this call.
        var passwordCorrect = passwordHasher.Verify(
            request.Password,
            account?.PasswordHash ?? passwordHasher.DummyHashForTimingParity);

        if (account is null || !account.CanSignIn)
        {
            // Same generic body whether the email is unknown, the password is wrong, or the account
            // cannot currently sign in (suspended/deactivated) — spec 6.1.11's identical-message rule,
            // extended: none of these states are ever distinguishable from outside.
            return GenericInvalidCredentials();
        }

        if (account.IsLockedAt(now))
        {
            if (!passwordCorrect)
            {
                // Human ruling (approved contract delta §2): a wrong password against a locked
                // account gets the ordinary generic 401, not 423 — an enumerator without the real
                // password learns nothing beyond "login details are not correct".
                return GenericInvalidCredentials();
            }

            // The submitted password IS correct: only now does the caller learn the account is
            // locked, and only the real account holder can ever see this response.
            return Result.Failure<SignInResult>(new LockedError(account.LockedUntilUtc!.Value));
        }

        if (!passwordCorrect)
        {
            account.RegisterFailedLogin(now);

            // Bypasses the ambient transaction deliberately — see the class remarks.
            await accounts.PersistLockoutStateAsync(account, cancellationToken).ConfigureAwait(false);

            return GenericInvalidCredentials();
        }

        account.RegisterSuccessfulLogin(now);

        await EvictOldestSessionIfAtCapacityAsync(account.Id, now, cancellationToken).ConfigureAwait(false);

        var rawToken = SessionTokens.GenerateRawToken();
        var session = AdminSession.Create(Guid.CreateVersion7(), account.Id, SessionTokens.HashToken(rawToken), now);
        await sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);

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
            session.IdleExpiresAtUtc,
            session.AbsoluteExpiresAtUtc);

        return Result.Success(new SignInResult(response, session.Id, rawToken));
    }

    private async Task EvictOldestSessionIfAtCapacityAsync(
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var active = await sessions.FindActiveForAccountAsync(accountId, now, cancellationToken)
            .ConfigureAwait(false);

        if (active.Count < AuthPolicy.MaxConcurrentSessions)
        {
            return;
        }

        // Oldest first (repository contract) — evict enough of the oldest to make room for the one
        // about to be created.
        var toEvict = active.Count - AuthPolicy.MaxConcurrentSessions + 1;

        for (var index = 0; index < toEvict; index++)
        {
            active[index].Revoke(now, AdminSessionRevocationReasons.SessionLimitExceeded);
        }
    }

    private static Result<SignInResult> GenericInvalidCredentials() =>
        Result.Failure<SignInResult>(Error.Unauthenticated(
            "auth.invalid_credentials",
            "Login details are not correct."));
}
