using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>GET /api/v1/config-versions</c> — the version history, cursor-paginated per spec 9.5 (never
/// offset), newest first.
/// </summary>
/// <param name="Cursor">
/// The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.
/// </param>
/// <param name="PageSize">Defaults to <see cref="CursorPageRequest.DefaultPageSize"/>, capped at <see cref="CursorPageRequest.MaxPageSize"/>.</param>
public sealed record ListConfigVersionsQuery(string? Cursor, int? PageSize)
    : IQuery<Result<CursorPage<ConfigVersionSummaryDto>>>;

/// <summary>
/// Validates <see cref="ListConfigVersionsQuery"/>. A malformed (but present) <c>PageSize</c> is
/// REJECTED with 422 here, matching the reference slice's own reasoning; a malformed cursor is a
/// business-shaped decode failure the handler reports instead, since decoding an opaque token is not
/// itself a "field is out of range" question FluentValidation is well suited to phrase.
/// </summary>
internal sealed class ListConfigVersionsQueryValidator : AbstractValidator<ListConfigVersionsQuery>
{
    public ListConfigVersionsQueryValidator()
    {
        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be 1 or greater.")
            .LessThanOrEqualTo(CursorPageRequest.MaxPageSize)
            .WithMessage($"PageSize must be at most {CursorPageRequest.MaxPageSize}.")
            .When(query => query.PageSize is not null);
    }
}
