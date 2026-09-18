using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// <c>GET /api/v1/pupils</c> (spec 6.5.15). Cursor-paginated per spec 9.5. Arm-scoped for a Class
/// Teacher (spec 6.5.3) — see <see cref="ListPupilsQueryHandler"/>'s remarks for why that check runs
/// in the handler rather than as a route-declarative scope.
/// </summary>
/// <param name="Cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
/// <param name="PageSize">Defaults to <see cref="CursorPageRequest.DefaultPageSize"/>, capped at <see cref="CursorPageRequest.MaxPageSize"/>.</param>
/// <param name="Status">
/// <see langword="null"/> excludes <see cref="PupilStatus.Pending"/> (the pending-exclusion
/// invariant's structural default). <see cref="PupilStatus.Pending"/> explicitly is the one opt-out
/// this endpoint accepts; any other explicit value is honoured exactly.
/// </param>
/// <param name="Search">
/// Matches any part of surname/first/middle name, or the registration number in full or its serial
/// alone (spec 6.5.15). Contact and authorised-pickup-person search are the next card's — not
/// covered here; see <see cref="PupilDto.MatchedField"/>'s own remarks.
/// </param>
public sealed record ListPupilsQuery(string? Cursor, int? PageSize, PupilStatus? Status, string? Search)
    : IQuery<Result<CursorPage<PupilDto>>>;

/// <summary>Validates <see cref="ListPupilsQuery"/>.</summary>
internal sealed class ListPupilsQueryValidator : AbstractValidator<ListPupilsQuery>
{
    private const int SearchMaxLength = 160;

    public ListPupilsQueryValidator()
    {
        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be 1 or greater.")
            .LessThanOrEqualTo(CursorPageRequest.MaxPageSize)
            .WithMessage($"PageSize must be at most {CursorPageRequest.MaxPageSize}.")
            .When(query => query.PageSize is not null);

        RuleFor(query => query.Status).IsInEnum();

        RuleFor(query => query.Search)
            .MaximumLength(SearchMaxLength)
            .When(query => query.Search is not null);
    }
}
