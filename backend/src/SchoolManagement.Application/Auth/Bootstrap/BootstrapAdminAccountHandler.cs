using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.Bootstrap;

/// <summary>Handles <see cref="BootstrapAdminAccountCommand"/>.</summary>
internal sealed class BootstrapAdminAccountHandler(
    IAdminAccountRepository accounts,
    IPasswordHasher passwordHasher)
    : IRequestHandler<BootstrapAdminAccountCommand, Result<BootstrapAdminAccountResponse>>
{
    /// <inheritdoc />
    public async Task<Result<BootstrapAdminAccountResponse>> HandleAsync(
        BootstrapAdminAccountCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await accounts.AnyExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            // Verbatim message from spec 6.1.6.
            return Result.Failure<BootstrapAdminAccountResponse>(Error.Conflict(
                "auth.bootstrap_already_run",
                "An administrator account already exists. Bootstrap has already run."));
        }

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var passwordHash = passwordHasher.Hash(temporaryPassword);

        var creation = AdminAccount.CreateBootstrapSuperAdmin(
            Guid.CreateVersion7(),
            request.Email,
            request.StaffName,
            passwordHash);

        if (creation.IsFailure)
        {
            return Result.Failure<BootstrapAdminAccountResponse>(creation.Error);
        }

        var account = creation.Value;

        // CreatedAtUtc/CreatedBy are stamped by AuditingInterceptor on save; setting them here would
        // be redundant and immediately overwritten.
        await accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);

        return Result.Success(new BootstrapAdminAccountResponse(
            account.Id.ToString(),
            account.Email,
            temporaryPassword));
    }
}
