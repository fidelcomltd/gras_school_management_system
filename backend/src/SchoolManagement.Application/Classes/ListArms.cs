using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>GET /api/v1/arms</c> (spec 6.4.5, 6.4.9): filtered by session, level, label and status; sorted by
/// the owning level's chain order, then label collated naturally. Cursor-paginated per spec 9.5.
/// </summary>
/// <param name="Cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
/// <param name="PageSize">Defaults to <see cref="CursorPageRequest.DefaultPageSize"/>, capped at <see cref="CursorPageRequest.MaxPageSize"/>.</param>
/// <param name="SessionId"><see langword="null"/> for every session.</param>
/// <param name="LevelId"><see langword="null"/> for every level.</param>
/// <param name="Label">Case-insensitive substring match against the composed display name (spec 6.4.5: "typing 2c finds Primary 2C").</param>
/// <param name="Status"><see langword="null"/> for every status.</param>
/// <param name="FormTeacherAdminId"><see langword="null"/> for every form teacher, including unassigned.</param>
public sealed record ListArmsQuery(
    string? Cursor,
    int? PageSize,
    string? SessionId,
    string? LevelId,
    string? Label,
    ArmStatus? Status,
    string? FormTeacherAdminId)
    : IQuery<Result<CursorPage<ArmDto>>>;

/// <summary>Validates <see cref="ListArmsQuery"/>.</summary>
internal sealed class ListArmsQueryValidator : AbstractValidator<ListArmsQuery>
{
    public ListArmsQueryValidator()
    {
        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(CursorPageRequest.MaxPageSize)
            .When(query => query.PageSize is not null);

        RuleFor(query => query.SessionId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("SessionId must be a valid identifier.")
            .When(query => query.SessionId is not null);

        RuleFor(query => query.LevelId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("LevelId must be a valid identifier.")
            .When(query => query.LevelId is not null);

        RuleFor(query => query.FormTeacherAdminId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("FormTeacherAdminId must be a valid identifier.")
            .When(query => query.FormTeacherAdminId is not null);
    }
}
