using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>Handles <see cref="RevokeAdminAccountSessionsCommand"/>.</summary>
internal sealed class RevokeAdminAccountSessionsCommandHandler(
    IAdminAccountRepository accounts,
    IAdminSessionRepository sessions,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<RevokeAdminAccountSessionsCommand, Result>
{
    private const string EntityType = "admin_account";

    /// <inheritdoc />
    public async Task<Result> HandleAsync(
        RevokeAdminAccountSessionsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var target = await accounts.FindReadOnlyByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (target is null)
        {
            return Result.Failure(Error.NotFound(
                "admin.not_found",
                "No admin account was found with that id."));
        }

        var now = timeProvider.GetUtcNow();
        var activeSessions = await sessions
            .FindActiveForAccountAsync(request.Id, now, cancellationToken)
            .ConfigureAwait(false);

        foreach (var session in activeSessions)
        {
            session.Revoke(now, AdminSessionRevocationReasons.RevokedByAdmin);
        }

        await auditSink.RecordAsync(
            Privileges.Admin.SessionRevoke,
            EntityType,
            request.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
