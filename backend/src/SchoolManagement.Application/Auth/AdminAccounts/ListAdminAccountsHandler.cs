using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>Handles <see cref="ListAdminAccountsQuery"/>.</summary>
internal sealed class ListAdminAccountsQueryHandler(IAdminAccountRepository accounts)
    : IRequestHandler<ListAdminAccountsQuery, Result<CursorPage<AdminAccountSummaryDto>>>
{
    /// <inheritdoc />
    public async Task<Result<CursorPage<AdminAccountSummaryDto>>> HandleAsync(
        ListAdminAccountsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Cursor is not null &&
            !AdminAccountListCursor.TryDecode(request.Cursor, out _, out _, out _))
        {
            return Result.Failure<CursorPage<AdminAccountSummaryDto>>(Error.Validation(
                "admins.invalid_cursor",
                "The cursor is invalid or has expired. Start again from the first page."));
        }

        var pageSize = Math.Clamp(
            request.PageSize ?? CursorPageRequest.DefaultPageSize,
            1,
            CursorPageRequest.MaxPageSize);

        var page = await accounts
            .ListAsync(request.Status, request.Search, request.Cursor, pageSize, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(page);
    }
}
