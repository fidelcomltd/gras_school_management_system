using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// <c>GET /api/v1/subjects</c> (spec 6.6.7, 6.6.9): "name, code, status, number of levels mapped this
/// term, number of arm exceptions this term, and number of pupils currently taking it... Filters:
/// status, level, term. Default sort by name ascending."
/// </summary>
/// <param name="Cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
/// <param name="PageSize">Defaults to <see cref="CursorPageRequest.DefaultPageSize"/>, capped at <see cref="CursorPageRequest.MaxPageSize"/>.</param>
/// <param name="Status"><see langword="null"/> for every status.</param>
/// <param name="LevelId">
/// <see langword="null"/> for every level. When supplied, only subjects with an active mapping to
/// that level are returned — restricted to <see cref="TermId"/> when that is also supplied, or any
/// term otherwise.
/// </param>
/// <param name="TermId">
/// <see langword="null"/> for none. TASK-0070 delta amendment 3: this endpoint never guesses a term
/// — with <see cref="TermId"/> absent, <see cref="SubjectDto.MappedLevelCount"/>,
/// <see cref="SubjectDto.ArmExceptionCount"/> and <see cref="SubjectDto.PupilsTakingCount"/> are all
/// <see langword="null"/> rather than resolved against a guessed term.
/// </param>
public sealed record ListSubjectsQuery(
    string? Cursor,
    int? PageSize,
    SubjectStatus? Status,
    string? LevelId,
    string? TermId)
    : IQuery<Result<CursorPage<SubjectDto>>>;

/// <summary>Validates <see cref="ListSubjectsQuery"/>.</summary>
internal sealed class ListSubjectsQueryValidator : AbstractValidator<ListSubjectsQuery>
{
    public ListSubjectsQueryValidator()
    {
        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(CursorPageRequest.MaxPageSize)
            .When(query => query.PageSize is not null);

        RuleFor(query => query.Status).IsInEnum().When(query => query.Status is not null);

        RuleFor(query => query.LevelId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("LevelId must be a valid identifier.")
            .When(query => query.LevelId is not null);

        RuleFor(query => query.TermId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.")
            .When(query => query.TermId is not null);
    }
}
