using System.Globalization;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Security.Assignments;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>Handles <see cref="ListAdminAccountsQuery"/>.</summary>
internal sealed class ListAdminAccountsQueryHandler(
    IAdminAccountRepository accounts,
    IRoleAssignmentRepository assignments,
    AssignmentNames assignmentNames)
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

        return Result.Success(page with { Items = await WithRolesAsync(page.Items, cancellationToken).ConfigureAwait(false) });
    }

    // Spec 6.1.8's "Roles held" and "Scope summary" columns (TASK-0046), from the page's active assignments in one read.
    private async Task<IReadOnlyList<AdminAccountSummaryDto>> WithRolesAsync(
        IReadOnlyList<AdminAccountSummaryDto> items, CancellationToken cancellationToken)
    {
        var ids = items.Select(item => Guid.Parse(item.Id)).ToArray();
        var active = await assignments.ListActiveForAccountsReadOnlyAsync(ids, cancellationToken).ConfigureAwait(false);
        var names = await assignmentNames.LoadAsync(active, cancellationToken).ConfigureAwait(false);
        var byAccount = active.ToLookup(assignment => assignment.AdminAccountId.ToString("D", CultureInfo.InvariantCulture));

        return items.Select(item =>
            {
                if (item.IsSuperAdmin)
                {
                    return item with { RolesHeld = [Role.ReservedName], ScopeSummary = SchoolWide };
                }

                // A Closed session's assignments reach only last year's records, so they are not what the account "holds".
                var current = byAccount[item.Id].Where(assignment => !names.IsClosed(assignment.SessionId)).ToList();
                return item with
                {
                    RolesHeld = [.. current.Select(assignment => names.Role(assignment.RoleId)).Distinct().Order(StringComparer.OrdinalIgnoreCase)],
                    ScopeSummary = ScopeSummary(current, names),
                };
            })
            .ToArray();
    }

    private const string SchoolWide = "School-wide";

    private static string ScopeSummary(List<RoleAssignment> held, AssignmentNameLookup names)
    {
        if (held.Any(assignment => assignment.ScopeType == ScopeType.SchoolWide))
        {
            return SchoolWide;
        }

        var classes = names.ClassesInOrder(held.SelectMany(assignment => assignment.ArmIds));
        return classes.Count switch
        {
            0 => string.Empty,
            1 => $"1 class: {classes[0]}",
            2 => $"2 classes: {classes[0]}, {classes[1]}",
            _ => string.Create(CultureInfo.InvariantCulture, $"{classes.Count} classes: {classes[0]}, {classes[1]}, …"),
        };
    }
}
