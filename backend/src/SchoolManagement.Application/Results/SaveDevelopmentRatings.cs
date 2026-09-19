using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// One submitted row of <see cref="SaveDevelopmentRatingsCommand"/> (TASK-0083 stage 2; human rulings
/// Q1-A and Q3-A, 2026-09-19).
/// </summary>
/// <param name="PupilId">Must be on the arm's active roster.</param>
/// <param name="Ratings">
/// Indicator id to a cell. Q1-A: a key OMITTED from this map leaves that indicator's existing rating
/// UNTOUCHED. A key present with an explicit JSON <see langword="null"/> value CLEARS (deletes) that
/// rating. A key present with <see cref="DevelopmentRatingCellDto.PointId"/> also
/// <see langword="null"/> is treated the SAME as an explicit null value — Q3-A's "clearing the point
/// clears the comment": whichever way the client expresses "no point", the whole cell (point and
/// comment) is deleted. A key present with a non-null <c>pointId</c> sets or replaces both the point
/// and the comment together — a whole-cell replace, never a sub-field patch of an existing comment.
/// </param>
public sealed record SaveDevelopmentRatingsRowInput(string PupilId, IReadOnlyDictionary<string, DevelopmentRatingCellDto?>? Ratings);

/// <summary>
/// <c>PUT /api/v1/arms/{armId}/development-ratings</c> (TASK-0083 stage 2) — partial-save grid write,
/// one transaction. The first save for an arm/term creates the result set (Draft), same convention as
/// <c>SaveTraitRatingsCommand</c>.
/// </summary>
/// <param name="ArmId">The arm this grid belongs to, from the route.</param>
/// <param name="TermId">The term this grid is for.</param>
/// <param name="Version">
/// The grid's version as last read, or <see langword="null"/> for a grid with no ratings yet. A
/// mismatch against the server's current version is a 409 <c>development_ratings.stale_version</c>.
/// </param>
/// <param name="Rows">Every row being saved. A row not present here is left entirely untouched.</param>
public sealed record SaveDevelopmentRatingsCommand(
    string ArmId, string TermId, string? Version, IReadOnlyList<SaveDevelopmentRatingsRowInput> Rows)
    : ICommand<Result<DevelopmentRatingSheetDto>>;

/// <summary>
/// Structural checks only — every data-dependent rule (spec §6.7.7: roster membership, indicator
/// existence/status, point-to-scale, comment-requires-point, comment-domain-allows-it) is the
/// handler's job, same split <c>SaveTraitRatingsCommandValidator</c> uses.
/// </summary>
internal sealed class SaveDevelopmentRatingsCommandValidator : AbstractValidator<SaveDevelopmentRatingsCommand>
{
    public SaveDevelopmentRatingsCommandValidator()
    {
        RuleFor(command => command.ArmId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");
        RuleFor(command => command.TermId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
        RuleFor(command => command.Rows).NotNull();

        RuleForEach(command => command.Rows).ChildRules(row =>
        {
            row.RuleFor(entry => entry.PupilId).NotEmpty().Must(value => Guid.TryParse(value, out _))
                .WithMessage("PupilId must be a valid identifier.");
            row.RuleFor(entry => entry.Ratings).NotNull()
                .WithMessage("Ratings must be supplied, even if empty.");
        });
    }
}
