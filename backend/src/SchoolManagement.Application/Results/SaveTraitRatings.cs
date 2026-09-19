using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// One submitted row of <see cref="SaveTraitRatingsCommand"/> (TASK-0083 stage 1; human ruling Q1-A,
/// 2026-09-19).
/// </summary>
/// <param name="PupilId">Must be on the arm's active roster.</param>
/// <param name="Ratings">
/// Trait id to point id. Q1-A: a key OMITTED from this map leaves that trait's existing rating
/// UNTOUCHED. A key present with an explicit <see langword="null"/> value CLEARS (deletes) that
/// rating. A key present with a point id sets or replaces it. A row can therefore touch as few or as
/// many traits as the caller intends — this is a partial save, unlike the score sheet's whole-row
/// <c>ComponentMarks</c>.
/// </param>
public sealed record SaveTraitRatingsRowInput(string PupilId, IReadOnlyDictionary<string, string?>? Ratings);

/// <summary>
/// <c>PUT /api/v1/arms/{armId}/trait-ratings</c> (TASK-0083 stage 1) — partial-save grid write, one
/// transaction. The first save for an arm/term creates the result set (Draft), same convention as
/// <c>SaveScoreSheetCommand</c>.
/// </summary>
/// <param name="ArmId">The arm this grid belongs to, from the route.</param>
/// <param name="TermId">The term this grid is for.</param>
/// <param name="Version">
/// The grid's version as last read, or <see langword="null"/> for a grid with no ratings yet. A
/// mismatch against the server's current version is a 409 <c>trait_ratings.stale_version</c>.
/// </param>
/// <param name="Rows">Every row being saved. A row not present here is left entirely untouched.</param>
public sealed record SaveTraitRatingsCommand(
    string ArmId, string TermId, string? Version, IReadOnlyList<SaveTraitRatingsRowInput> Rows)
    : ICommand<Result<TraitRatingSheetDto>>;

/// <summary>
/// Structural checks only — every data-dependent rule (spec §6.7.7: roster membership, trait
/// existence/status, point-to-scale) is the handler's job, same split <c>SaveScoreSheetCommandValidator</c> uses.
/// </summary>
internal sealed class SaveTraitRatingsCommandValidator : AbstractValidator<SaveTraitRatingsCommand>
{
    public SaveTraitRatingsCommandValidator()
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
