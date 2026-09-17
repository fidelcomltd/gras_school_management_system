using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// <c>POST /api/v1/subject-mappings/prefill</c> (TASK-0070 delta amendment 5, human-ruled
/// 2026-09-16 — not in spec 6.6.9's own endpoint list). Applies the school's standard 14/19 subject
/// list to a named term using the per-section sheet order (spec appendices E and F) as
/// <c>display_order</c>. Additive only — can never end a mapping — and never duplicates an existing
/// active one. Requires <c>subject.map</c> only; <c>subject.unmap</c> is never demanded because this
/// action cannot end anything.
/// </summary>
/// <param name="TermId">The term to prefill.</param>
/// <param name="DryRun">When <see langword="true"/>, computes and returns the preview but writes nothing. The closed-term refusal applies here too.</param>
public sealed record PrefillSubjectMappingsCommand(string TermId, bool DryRun)
    : ICommand<Result<SaveSubjectMappingGridResponse>>;

/// <summary>Structural checks only.</summary>
internal sealed class PrefillSubjectMappingsCommandValidator : AbstractValidator<PrefillSubjectMappingsCommand>
{
    public PrefillSubjectMappingsCommandValidator() =>
        RuleFor(command => command.TermId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
}
