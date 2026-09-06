using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>Handles <see cref="CreateAdminAccountCommand"/>.</summary>
internal sealed class CreateAdminAccountCommandHandler(
    IAdminAccountRepository accounts,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<CreateAdminAccountCommand, Result<CreateAdminAccountResponse>>
{
    private const string EntityType = "admin_account";

    /// <inheritdoc />
    public async Task<Result<CreateAdminAccountResponse>> HandleAsync(
        CreateAdminAccountCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Pre-checked rather than relying on the unique index alone (AGENTS.md §4's recipe) — a clean
        // 409 with a stable error code beats translating a constraint-violation exception.
        if (await accounts.EmailExistsActiveOrSuspendedAsync(normalizedEmail, excludingId: null, cancellationToken)
            .ConfigureAwait(false))
        {
            return Result.Failure<CreateAdminAccountResponse>(Error.Conflict(
                "admin.email_taken",
                "An active or suspended account already uses this email."));
        }

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var passwordHash = passwordHasher.Hash(temporaryPassword);

        var creation = AdminAccount.Create(
            Guid.CreateVersion7(),
            request.Email,
            request.StaffName,
            request.Phone,
            passwordHash);

        if (creation.IsFailure)
        {
            return Result.Failure<CreateAdminAccountResponse>(creation.Error);
        }

        var account = creation.Value;
        await accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Admin.Create,
            EntityType,
            account.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new CreateAdminAccountResponse(
            account.Id.ToString("D", CultureInfo.InvariantCulture),
            account.StaffName,
            account.Email,
            account.Phone!,
            account.Status,
            account.MustChangePassword,
            now,
            temporaryPassword));
    }
}
