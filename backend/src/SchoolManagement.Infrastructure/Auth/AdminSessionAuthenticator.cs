using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Auth;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Auth;

/// <summary>EF Core implementation of <see cref="IAdminSessionAuthenticator"/>.</summary>
/// <remarks>
/// Runs on the hot path of every authenticated request — one <c>AsNoTracking</c> query, joining
/// <c>admin_sessions</c> to <c>admin_accounts</c> by the token's SHA-256 hash, projected straight to
/// the fields the authentication handler needs. Never writes anything.
/// </remarks>
internal sealed class AdminSessionAuthenticator(ApplicationDbContext context, TimeProvider timeProvider)
    : IAdminSessionAuthenticator
{
    /// <inheritdoc />
    public async Task<AdminSessionAuthenticationResult> AuthenticateAsync(
        string rawSessionToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawSessionToken);

        var tokenHash = SessionTokens.HashToken(rawSessionToken);

        var projection = await context.AdminSessions
            .AsNoTracking()
            .Where(session => session.TokenHash == tokenHash)
            .Join(
                context.AdminAccounts.AsNoTracking(),
                session => session.AdminAccountId,
                account => account.Id,
                (session, account) => new
                {
                    session.Id,
                    AccountId = account.Id,
                    account.Email,
                    account.StaffName,
                    account.IsSuperAdmin,
                    account.MustChangePassword,
                    session.RevokedAtUtc,
                    session.IdleExpiresAtUtc,
                    session.AbsoluteExpiresAtUtc,
                })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (projection is null)
        {
            return AdminSessionAuthenticationResult.NotFound;
        }

        if (projection.RevokedAtUtc is not null)
        {
            return AdminSessionAuthenticationResult.Revoked;
        }

        var now = timeProvider.GetUtcNow();

        if (now >= projection.IdleExpiresAtUtc || now >= projection.AbsoluteExpiresAtUtc)
        {
            return AdminSessionAuthenticationResult.Expired;
        }

        return new AdminSessionAuthenticationResult(
            AdminSessionAuthenticationOutcome.Valid,
            SessionId: projection.Id,
            AccountId: projection.AccountId,
            Email: projection.Email,
            StaffName: projection.StaffName,
            IsSuperAdmin: projection.IsSuperAdmin,
            MustChangePassword: projection.MustChangePassword);
    }
}
