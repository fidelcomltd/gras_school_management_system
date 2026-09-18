using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary><c>POST /api/v1/sections</c> (spec 6.4.9).</summary>
/// <param name="Name">2..40 characters, trimmed. Unique, case-insensitive.</param>
public sealed record CreateSectionCommand(string Name) : ICommand<Result<SectionDto>>;

/// <summary>Structural check only — the real length/uniqueness rules live in <see cref="Section.Create"/>.</summary>
internal sealed class CreateSectionCommandValidator : AbstractValidator<CreateSectionCommand>
{
    public CreateSectionCommandValidator() =>
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Section.NameMaxLength);
}
