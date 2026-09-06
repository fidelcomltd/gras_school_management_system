using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>Handles <see cref="ResetAdminAccountPasswordCommand"/>.</summary>
internal sealed class ResetAdminAccountPasswordCommandHandler(
    IAdminAccountRepository accounts,
    IAdminSessionRepository sessions,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<ResetAdminAccountPasswordCommand, Result<ResetAdminAccountPasswordResponse>>
{
    private const string EntityType = "admin_account";

    /// <inheritdoc />
    public async Task<Result<ResetAdminAccountPasswordResponse>> HandleAsync(
        ResetAdminAccountPasswordCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var target = await accounts.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (target is null)
        {
            return Result.Failure<ResetAdminAccountPasswordResponse>(Error.NotFound(
                "admin.not_found",
                "No admin account was found with that id."));
        }

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        target.ForcePasswordReset(passwordHasher.Hash(temporaryPassword));

        // Spec 6.1.11: "revokes every active session for the account" — every one, unlike a
        // self-service change, which spares the session performing it (there is no such session
        // here: the acting admin and the target are different accounts by construction, since
        // admin.password.reset against oneself has no self-carve-out in the approved delta).
        var now = timeProvider.GetUtcNow();
        var activeSessions = await sessions
            .FindActiveForAccountAsync(target.Id, now, cancellationToken)
            .ConfigureAwait(false);

        foreach (var session in activeSessions)
        {
            session.Revoke(now, AdminSessionRevocationReasons.ForcedPasswordReset);
        }

        await auditSink.RecordAsync(
            Privileges.Admin.PasswordReset,
            EntityType,
            target.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new ResetAdminAccountPasswordResponse(
            target.Id.ToString("D", CultureInfo.InvariantCulture),
            temporaryPassword));
    }
}
