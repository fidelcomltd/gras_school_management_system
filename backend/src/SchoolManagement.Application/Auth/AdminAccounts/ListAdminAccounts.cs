using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>
/// <c>GET /api/v1/admins</c> (spec 6.1.8), cursor-paginated per spec 9.5 — never offset. Role, scope
/// arm and session filters are TASK-0028 (approved delta, entry `TASK-0019/0027`, B4) — this card
/// implements only <paramref name="Status"/> and free-text <paramref name="Search"/>.
/// </summary>
/// <param name="Cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
/// <param name="PageSize">Defaults to <see cref="CursorPageRequest.DefaultPageSize"/>, capped at <see cref="CursorPageRequest.MaxPageSize"/>.</param>
/// <param name="Status">
/// <see langword="null"/> to use spec 6.1.8's default (active and suspended, deactivated excluded).
/// Any explicit value — including <see cref="AdminAccountStatus.Deactivated"/> — is honoured exactly.
/// </param>
/// <param name="Search">Case-insensitive substring match against staff name or email, or <see langword="null"/>.</param>
public sealed record ListAdminAccountsQuery(
    string? Cursor,
    int? PageSize,
    AdminAccountStatus? Status,
    string? Search)
    : IQuery<Result<CursorPage<AdminAccountSummaryDto>>>;

/// <summary>Validates <see cref="ListAdminAccountsQuery"/>.</summary>
internal sealed class ListAdminAccountsQueryValidator : AbstractValidator<ListAdminAccountsQuery>
{
    /// <summary>Free-text search is bounded generously; nothing in spec 9.5 fixes a number.</summary>
    private const int SearchMaxLength = 160;

    public ListAdminAccountsQueryValidator()
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
    }
}
