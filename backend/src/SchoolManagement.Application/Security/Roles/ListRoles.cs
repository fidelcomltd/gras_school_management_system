using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Roles;

/// <summary>
/// <c>GET /api/v1/roles</c> (spec 9.5, page size 25; approved delta entry <c>TASK-0028</c> §2).
/// Archived roles are excluded from the default list (spec 9.4's default-scope rule, applied at the
/// data-access layer — see <see cref="Abstractions.Security.IRoleRepository.ListAsync"/>); the
/// <paramref name="Status"/> filter opts them back in.
/// </summary>
/// <param name="Cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
/// <param name="PageSize">Defaults to <see cref="CursorPageRequest.DefaultPageSize"/>, capped at <see cref="CursorPageRequest.MaxPageSize"/>.</param>
/// <param name="Status"><see langword="null"/> to use the default (active only).</param>
/// <param name="Search">Case-insensitive substring match against name, or <see langword="null"/>.</param>
/// <param name="Sort">Whitelist: <c>name</c> or <c>status</c>. Anything else falls back to <c>name</c>.</param>
/// <param name="Direction"><c>asc</c> (default) or <c>desc</c>. Anything else falls back to ascending.</param>
public sealed record ListRolesQuery(
    string? Cursor,
    int? PageSize,
    RoleStatus? Status,
    string? Search,
    string? Sort,
    string? Direction)
    : IQuery<Result<CursorPage<RoleDto>>>;

/// <summary>Validates <see cref="ListRolesQuery"/>.</summary>
internal sealed class ListRolesQueryValidator : AbstractValidator<ListRolesQuery>
{
    /// <summary>Free-text search is bounded generously; nothing in spec 9.5 fixes a number.</summary>
    private const int SearchMaxLength = 160;

    private static readonly string[] SortWhitelist = ["name", "status"];
    private static readonly string[] DirectionWhitelist = ["asc", "desc"];

    public ListRolesQueryValidator()
    {
        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be 1 or greater.")
            .LessThanOrEqualTo(CursorPageRequest.MaxPageSize)
            .WithMessage($"PageSize must be at most {CursorPageRequest.MaxPageSize}.")
            .When(query => query.PageSize is not null);

        RuleFor(query => query.Status)
            .IsInEnum();

        RuleFor(query => query.Search)
            .MaximumLength(SearchMaxLength)
            .When(query => query.Search is not null);

        RuleFor(query => query.Sort)
            .Must(sort => SortWhitelist.Contains(sort, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Sort must be either 'name' or 'status'.")
            .When(query => query.Sort is not null);

        RuleFor(query => query.Direction)
            .Must(direction => DirectionWhitelist.Contains(direction, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Direction must be either 'asc' or 'desc'.")
            .When(query => query.Direction is not null);
    }
}
