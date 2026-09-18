using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// <c>POST /api/v1/subject-mappings/copy</c> (spec 6.6.5, 6.6.9): "Body carries source term and
/// destination term." Additive only — this card's own judgement call (see the handler's remarks):
/// the one worked example spec 6.6.8 gives ("a term with 14 mappings into a term that already has 3 →
/// 11 additions, 0 endings, does not duplicate the 3") never shows an ending, so copy never ends a
/// destination mapping the source term does not have. Requires <c>subject.map</c> only.
/// </summary>
/// <param name="SourceTermId">Copy FROM this term's currently active mappings.</param>
/// <param name="DestinationTermId">Copy INTO this term.</param>
/// <param name="DryRun">When <see langword="true"/>, computes and returns the preview but writes nothing.</param>
public sealed record CopySubjectMappingsCommand(string SourceTermId, string DestinationTermId, bool DryRun)
    : ICommand<Result<SaveSubjectMappingGridResponse>>;

/// <summary>Structural checks only.</summary>
internal sealed class CopySubjectMappingsCommandValidator : AbstractValidator<CopySubjectMappingsCommand>
{
    public CopySubjectMappingsCommandValidator()
    {
        RuleFor(command => command.SourceTermId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("SourceTermId must be a valid identifier.");

        RuleFor(command => command.DestinationTermId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("DestinationTermId must be a valid identifier.");

        RuleFor(command => command)
            .Must(command => !string.Equals(command.SourceTermId, command.DestinationTermId, StringComparison.OrdinalIgnoreCase))
            .WithMessage("SourceTermId and DestinationTermId must be different.")
            .WithName(nameof(CopySubjectMappingsCommand.DestinationTermId));
    }
}
