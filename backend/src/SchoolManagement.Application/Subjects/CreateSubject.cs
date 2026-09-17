using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// <c>POST /api/v1/subjects</c> (spec 6.6.2, 6.6.9).
/// </summary>
/// <param name="Name">1..80 characters. Unique, case-insensitive.</param>
/// <param name="Code">
/// <see langword="null"/> or empty for none — TASK-0070 delta amendment 1 makes this OPTIONAL,
/// departing from spec 6.6.2's <c>Req: Yes</c> (see the card's "Why code is optional"). Uppercase
/// letters and digits only when supplied; unique where supplied.
/// </param>
/// <param name="Description">Optional, up to 300 characters. Never printed.</param>
public sealed record CreateSubjectCommand(string Name, string? Code, string? Description)
    : ICommand<Result<SubjectDto>>;

/// <summary>Structural checks only — uniqueness lives in the handler.</summary>
internal sealed class CreateSubjectCommandValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(Subject.NameMaxLength);

        RuleFor(command => command.Code)
            .MaximumLength(Subject.CodeMaxLength)
            .Matches("^[A-Z0-9]+$")
            .WithMessage("Code may contain only uppercase letters and digits.")
            .When(command => !string.IsNullOrEmpty(command.Code));

        RuleFor(command => command.Description)
            .MaximumLength(Subject.DescriptionMaxLength)
            .When(command => command.Description is not null);
    }
}
