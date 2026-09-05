using FluentValidation;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.Refresh;

/// <summary>
/// Proactively extends the idle window of the caller's current session (approved contract delta §3a:
/// a PROACTIVE, pre-expiry keepalive — a reactive 401 of any variant is terminal and never routes
/// here). Approved contract delta: <c>POST /api/v1/auth/refresh</c>. Does not rotate the session
/// token (spec 9.1 rotates only on privilege and password change).
/// </summary>
public sealed record RefreshSessionCommand : ICommand<Result<AuthSessionResponse>>;

/// <summary>Validates <see cref="RefreshSessionCommand"/>. Empty — see <c>SignOutCommandValidator</c>'s remarks.</summary>
internal sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>;

/// <summary>Handles <see cref="RefreshSessionCommand"/>.</summary>
internal sealed class RefreshSessionCommandHandler(
    IAdminAccountRepository accounts,
    IAdminSessionRepository sessions,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentSession currentSession,
    TimeProvider timeProvider)
    : IRequestHandler<RefreshSessionCommand, Result<AuthSessionResponse>>
{
    /// <inheritdoc />
    public async Task<Result<AuthSessionResponse>> HandleAsync(
        RefreshSessionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentSession.SessionId is not { } sessionId)
        {
            return Result.Failure<AuthSessionResponse>(Error.Unauthenticated(
                "authentication.required",
                "Sign in to perform this action."));
        }

        var session = await sessions.FindTrackedByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (session is null)
        {
            return Result.Failure<AuthSessionResponse>(Error.Unauthenticated(
                "authentication.required",
                "Sign in to perform this action."));
        }

        var account = await accounts.FindTrackedByIdAsync(session.AdminAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return Result.Failure<AuthSessionResponse>(Error.Unauthenticated(
                "authentication.session_revoked",
                "Your access has been withdrawn. Contact the school administrator."));
        }

        var now = timeProvider.GetUtcNow();
        session.ExtendIdle(now);

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

        return Result.Success(response);
    }
}
