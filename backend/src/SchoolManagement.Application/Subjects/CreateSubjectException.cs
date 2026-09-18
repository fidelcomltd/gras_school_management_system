using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// <c>POST /api/v1/arms/{id}/subject-exceptions</c> (spec 6.6.4, 6.6.9). Requires <c>subject.map.arm</c>.
/// <c>Idempotency-Key</c> is REQUIRED (standing obligation: every retry-duplicable mutation declares
/// one — TASK-0070's own flagged gap beyond the card's original four examples).
/// </summary>
/// <param name="ArmId">Bound from the route. The one arm this exception applies to.</param>
/// <param name="SubjectId">Must reference an active subject.</param>
/// <param name="TermId">Must belong to the arm's session.</param>
/// <param name="Mode">include or exclude.</param>
/// <param name="Reason">1..200 characters. Required.</param>
public sealed record CreateSubjectExceptionCommand(
    string ArmId, string SubjectId, string TermId, SubjectExceptionMode Mode, string Reason)
    : ICommand<Result<SubjectExceptionDto>>;

/// <summary>Structural checks only — existence, session/term matching and the redundancy rule live in the handler.</summary>
internal sealed class CreateSubjectExceptionCommandValidator : AbstractValidator<CreateSubjectExceptionCommand>
{
    public CreateSubjectExceptionCommandValidator()
    {
        RuleFor(command => command.ArmId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");

        RuleFor(command => command.SubjectId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("SubjectId must be a valid identifier.");

        RuleFor(command => command.TermId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");

        RuleFor(command => command.Mode).IsInEnum();

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(SubjectMappingException.ReasonMaxLength);
    }
}
