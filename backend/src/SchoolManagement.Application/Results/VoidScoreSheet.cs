using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>The count of marks voided by <see cref="VoidScoreSheetCommand"/>.</summary>
/// <param name="VoidedCount">How many non-voided marks were voided. Zero when none existed.</param>
public sealed record VoidScoreSheetResponse(int VoidedCount);

/// <summary>
/// <c>POST /api/v1/arms/{armId}/score-sheets/void</c> (spec 6.7.4, 6.7.11; TASK-0076's approved
/// contract delta) — voids every non-voided mark for one arm, subject and term. Super Admin only,
/// reason required. Used only to unwind an error.
/// </summary>
/// <param name="ArmId">The arm this sheet belongs to, from the route.</param>
/// <param name="SubjectId">The subject to void.</param>
/// <param name="TermId">The term to void.</param>
/// <param name="Reason">Required, spec's ten-to-five-hundred-character reason floor.</param>
public sealed record VoidScoreSheetCommand(string ArmId, string SubjectId, string TermId, string Reason)
    : ICommand<Result<VoidScoreSheetResponse>>;

/// <summary>Structural checks only.</summary>
internal sealed class VoidScoreSheetCommandValidator : AbstractValidator<VoidScoreSheetCommand>
{
    public VoidScoreSheetCommandValidator()
    {
        RuleFor(command => command.ArmId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");
        RuleFor(command => command.SubjectId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("SubjectId must be a valid identifier.");
        RuleFor(command => command.TermId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
        RuleFor(command => command.Reason)
            .NotEmpty()
            .MinimumLength(SubjectScore.VoidReasonMinLength)
            .MaximumLength(SubjectScore.VoidReasonMaxLength);
    }
}
