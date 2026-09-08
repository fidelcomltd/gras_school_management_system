using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>GET /api/v1/levels</c> (spec 6.4.9): "Active by default, all with a flag." Ordered by
/// <c>progressionOrder</c> ascending. Cursor-paginated per spec 9.5 like every other list endpoint,
/// even though the list is short (card's own instruction — one pagination shape for the whole API).
/// </summary>
/// <param name="Cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
/// <param name="PageSize">Defaults to <see cref="CursorPageRequest.DefaultPageSize"/>, capped at <see cref="CursorPageRequest.MaxPageSize"/>.</param>
/// <param name="IncludeInactive"><see langword="true"/> when the request's <c>status</c> query value is <c>"all"</c>.</param>
public sealed record ListLevelsQuery(string? Cursor, int? PageSize, bool IncludeInactive)
    : IQuery<Result<CursorPage<LevelDto>>>;

/// <summary>Validates <see cref="ListLevelsQuery"/>.</summary>
internal sealed class ListLevelsQueryValidator : AbstractValidator<ListLevelsQuery>
{
    public ListLevelsQueryValidator() =>
        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be 1 or greater.")
            .LessThanOrEqualTo(CursorPageRequest.MaxPageSize)
            .WithMessage($"PageSize must be at most {CursorPageRequest.MaxPageSize}.")
            .When(query => query.PageSize is not null);
}
