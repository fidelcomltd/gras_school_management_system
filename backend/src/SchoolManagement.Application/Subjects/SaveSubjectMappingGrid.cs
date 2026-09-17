using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Subjects;

/// <summary>One ticked cell submitted to <see cref="SaveSubjectMappingGridCommand"/>.</summary>
/// <param name="SubjectId">Must reference an active subject.</param>
/// <param name="ClassLevelId">Must reference an active level.</param>
/// <param name="DisplayOrder">Row order of the subject on this level's result sheet (spec 6.6.3).</param>
public sealed record SubjectMappingGridEntryInput(string SubjectId, string ClassLevelId, int DisplayOrder);

/// <summary>
/// <c>PUT /api/v1/subject-mappings?term_id=</c> (spec 6.6.5, 6.6.9): "Whole-grid save. Atomic. Supports
/// dry_run returning the additions and endings summary." TASK-0070 delta amendment 2: requires
/// <c>Subject.Map</c> when the computed diff contains additions and <c>Subject.Unmap</c> when it
/// contains endings — both when it contains both, checked in the handler because it is data-dependent.
/// </summary>
/// <param name="TermId">Bound from the <c>term_id</c> query string, not the body.</param>
/// <param name="Entries">The FULL desired grid for this term — every entry not currently active becomes an addition; every currently active pair absent from this list becomes an ending.</param>
/// <param name="DryRun">When <see langword="true"/>, computes and returns the preview but writes nothing. Evaluated against the SAME privilege rule as a real save (delta amendment 2).</param>
public sealed record SaveSubjectMappingGridCommand(
    string TermId,
    IReadOnlyList<SubjectMappingGridEntryInput> Entries,
    bool DryRun)
    : ICommand<Result<SaveSubjectMappingGridResponse>>;

/// <summary>Structural checks only — subject/level existence and state, and the privilege split, live in the handler.</summary>
internal sealed class SaveSubjectMappingGridCommandValidator : AbstractValidator<SaveSubjectMappingGridCommand>
{
    public SaveSubjectMappingGridCommandValidator()
    {
        RuleFor(command => command.TermId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");

        RuleFor(command => command.Entries).NotNull();

        RuleForEach(command => command.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.SubjectId)
                .NotEmpty()
                .Must(value => Guid.TryParse(value, out _))
                .WithMessage("SubjectId must be a valid identifier.");

            entry.RuleFor(e => e.ClassLevelId)
                .NotEmpty()
                .Must(value => Guid.TryParse(value, out _))
                .WithMessage("ClassLevelId must be a valid identifier.");

            entry.RuleFor(e => e.DisplayOrder).GreaterThanOrEqualTo(1);
        });

        RuleFor(command => command.Entries)
            .Must(entries => entries
                .Select(entry => (entry.SubjectId, entry.ClassLevelId))
                .Distinct()
                .Count() == entries.Count)
            .WithMessage("Each subject may appear at most once per level.")
            .When(command => command.Entries is { Count: > 0 });
    }
}
