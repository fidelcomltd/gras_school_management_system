using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>Handles <see cref="GetAdminAccountQuery"/>.</summary>
internal sealed class GetAdminAccountQueryHandler(IAdminAccountRepository accounts)
    : IRequestHandler<GetAdminAccountQuery, Result<AdminAccountDetailDto>>
{
    /// <inheritdoc />
    public async Task<Result<AdminAccountDetailDto>> HandleAsync(
        GetAdminAccountQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var account = await accounts.FindReadOnlyByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            return Result.Failure<AdminAccountDetailDto>(Error.NotFound(
                "admin.not_found",
                "No admin account was found with that id."));
        }

        return Result.Success(AdminAccountMapper.ToDetailDto(account));
    }
}
