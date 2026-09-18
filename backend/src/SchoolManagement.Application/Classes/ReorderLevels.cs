using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>POST /api/v1/levels/reorder</c> (spec 6.4.2, 6.4.9): "Whole ordered array of level ids. Atomic.
/// Rewrites progressionOrder and infers nextLevelId from adjacency." Used by the drag-and-drop list.
/// </summary>
/// <param name="OrderedLevelIds">Every currently ACTIVE level's id, in the new order. No duplicates.</param>
public sealed record ReorderLevelsCommand(IReadOnlyList<string> OrderedLevelIds)
    : ICommand<Result<IReadOnlyList<LevelDto>>>;

/// <summary>Structural checks only — the exact-set-of-active-levels rule lives in the handler.</summary>
internal sealed class ReorderLevelsCommandValidator : AbstractValidator<ReorderLevelsCommand>
{
    public ReorderLevelsCommandValidator()
    {
        RuleFor(command => command.OrderedLevelIds).NotEmpty();

        RuleForEach(command => command.OrderedLevelIds)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("Every id must be a valid identifier.");

        RuleFor(command => command.OrderedLevelIds)
            .Must(ids => ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() == ids.Count)
            .WithMessage("The ordered list cannot repeat an id.");
    }
}
