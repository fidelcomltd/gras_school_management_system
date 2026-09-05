using FluentValidation;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.MyAccount;

/// <summary>
/// Reports the caller's own account and session state (spec 6.1.14). Approved contract delta:
/// <c>GET /api/v1/auth/me</c>. Replaces <c>GET /api/v1/reference/whoami</c> (BREAKING removal — see
/// the approved delta §4): unlike <c>whoami</c>, this 401s when anonymous rather than tolerating it.
/// </summary>
/// <remarks>
/// ALWAYS <c>200</c> once authenticated, even while <c>mustChangePassword</c> is true — the
/// must-change-password gate middleware exempts this route explicitly (approved delta §2a), because
/// it is how the frontend learns the flag in the first place on a hard page reload.
/// </remarks>
public sealed record MeQuery : IQuery<Result<AuthSessionResponse>>;

/// <summary>Validates <see cref="MeQuery"/>. Empty — see <c>SignOutCommandValidator</c>'s remarks.</summary>
internal sealed class MeQueryValidator : AbstractValidator<MeQuery>;

/// <summary>Handles <see cref="MeQuery"/>.</summary>
internal sealed class MeQueryHandler(
    IAdminAccountRepository accounts,
    IAdminSessionRepository sessions,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ICurrentSession currentSession)
    : IRequestHandler<MeQuery, Result<AuthSessionResponse>>
{
    /// <inheritdoc />
    public async Task<Result<AuthSessionResponse>> HandleAsync(MeQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userIdText ||
            !Guid.TryParse(userIdText, out var accountId) ||
            currentSession.SessionId is not { } sessionId)
        {
            return Result.Failure<AuthSessionResponse>(Error.Unauthenticated(
                "authentication.required",
                "Sign in to perform this action."));
        }

        var account = await accounts.FindReadOnlyByIdAsync(accountId, cancellationToken).ConfigureAwait(false);
        var session = await sessions.FindReadOnlyByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (account is null || session is null)
        {
            return Result.Failure<AuthSessionResponse>(Error.Unauthenticated(
                "authentication.session_revoked",
                "Your access has been withdrawn. Contact the school administrator."));
        }

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
