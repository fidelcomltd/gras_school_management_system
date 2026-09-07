using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Security.Roles;

/// <summary>Handles <see cref="ListRolesQuery"/>.</summary>
internal sealed class ListRolesQueryHandler(IRoleRepository roles)
    : IRequestHandler<ListRolesQuery, Result<CursorPage<RoleDto>>>
{
    /// <inheritdoc />
    public async Task<Result<CursorPage<RoleDto>>> HandleAsync(
        ListRolesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Cursor is not null && !RoleListCursor.TryDecode(request.Cursor, out _, out _))
        {
            return Result.Failure<CursorPage<RoleDto>>(Error.Validation(
                "roles.invalid_cursor",
                "The cursor is invalid or has expired. Start again from the first page."));
        }

        var pageSize = Math.Clamp(
            request.PageSize ?? CursorPageRequest.DefaultPageSize,
            1,
            CursorPageRequest.MaxPageSize);

        var sort = string.Equals(request.Sort, "status", StringComparison.OrdinalIgnoreCase) ? "status" : "name";
        var descending = string.Equals(request.Direction, "desc", StringComparison.OrdinalIgnoreCase);

        var page = await roles
            .ListAsync(request.Status, request.Search, sort, descending, request.Cursor, pageSize, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(page);
    }
}
