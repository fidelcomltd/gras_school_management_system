using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Sessions;

/// <summary>
/// <c>GET /api/v1/sessions</c> (spec 6.3.8): "The session list is short and always will be... Default
/// sort is session name descending, newest first. No filters beyond state." Cursor-paginated per spec
/// 9.5 anyway — one pagination shape for the whole API (§8) — even though offset would never
/// realistically be noticed on this list.
/// </summary>
/// <param name="Cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
/// <param name="PageSize">Defaults to <see cref="CursorPageRequest.DefaultPageSize"/>, capped at <see cref="CursorPageRequest.MaxPageSize"/>.</param>
/// <param name="State"><see langword="null"/> for every state.</param>
public sealed record ListSessionsQuery(string? Cursor, int? PageSize, SessionState? State)
    : IQuery<Result<CursorPage<SessionDto>>>;

/// <summary>Validates <see cref="ListSessionsQuery"/>.</summary>
internal sealed class ListSessionsQueryValidator : AbstractValidator<ListSessionsQuery>
{
    public ListSessionsQueryValidator()
    {
        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be 1 or greater.")
            .LessThanOrEqualTo(CursorPageRequest.MaxPageSize)
            .WithMessage($"PageSize must be at most {CursorPageRequest.MaxPageSize}.")
            .When(query => query.PageSize is not null);

        RuleFor(query => query.State).IsInEnum();
    }
}
