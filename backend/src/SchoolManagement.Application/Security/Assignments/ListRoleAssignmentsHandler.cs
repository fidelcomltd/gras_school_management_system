using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Security.Assignments;

/// <summary>Handles <see cref="ListRoleAssignmentsQuery"/>.</summary>
internal sealed class ListRoleAssignmentsQueryHandler(
    IAdminAccountRepository accounts,
    IRoleAssignmentRepository assignments)
    : IRequestHandler<ListRoleAssignmentsQuery, Result<IReadOnlyList<RoleAssignmentDto>>>
{
    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<RoleAssignmentDto>>> HandleAsync(
        ListRoleAssignmentsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accountId = Guid.Parse(request.AdminAccountId);

        var account = await accounts.FindReadOnlyByIdAsync(accountId, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            return Result.Failure<IReadOnlyList<RoleAssignmentDto>>(Error.NotFound(
                "admin.not_found", "No admin account was found with that id."));
        }

        var rows = await assignments.ListForAccountReadOnlyAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<RoleAssignmentDto> items = rows.Select(RoleAssignmentMapper.ToDto).ToArray();

        return Result.Success(items);
    }
}
