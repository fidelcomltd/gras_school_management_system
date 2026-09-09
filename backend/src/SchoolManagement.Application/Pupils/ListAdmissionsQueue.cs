using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// <c>GET /api/v1/admissions</c> (spec 6.5.14, 6.5.17): the pending queue, and the only surface that
/// shows <c>pending</c> unconditionally (<c>GET /pupils</c> can also show it, but only when a caller
/// explicitly asks via <c>status=pending</c>). Gated <c>pupil.view</c>, SCHOOL-WIDE only — a pending
/// record has no arm yet (see <c>Pupil</c>'s own remarks), so an arm-scoped grant has no meaningful
/// reach here and this route is declared with <c>ScopeParameterKind.None</c> rather than the
/// data-dependent pattern <c>ListPupils</c> needs.
/// </summary>
/// <param name="Cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
/// <param name="PageSize">Defaults to <see cref="CursorPageRequest.DefaultPageSize"/>, capped at <see cref="CursorPageRequest.MaxPageSize"/>.</param>
public sealed record ListAdmissionsQueueQuery(string? Cursor, int? PageSize) : IQuery<Result<CursorPage<PupilDto>>>;

/// <summary>Validates <see cref="ListAdmissionsQueueQuery"/>.</summary>
internal sealed class ListAdmissionsQueueQueryValidator : AbstractValidator<ListAdmissionsQueueQuery>
{
    public ListAdmissionsQueueQueryValidator() =>
        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be 1 or greater.")
            .LessThanOrEqualTo(CursorPageRequest.MaxPageSize)
            .WithMessage($"PageSize must be at most {CursorPageRequest.MaxPageSize}.")
            .When(query => query.PageSize is not null);
}
