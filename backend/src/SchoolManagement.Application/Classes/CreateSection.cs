using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary><c>POST /api/v1/sections</c> (spec 6.4.9).</summary>
/// <param name="Name">2..40 characters, trimmed. Unique, case-insensitive.</param>
/// <param name="RatesTraits">
/// TASK-0083 ruling R1, additive. Optional — <see langword="null"/> (an existing caller that omits
/// the field) defaults to <see langword="false"/>, same as a section created before this field
/// existed.
/// </param>
public sealed record CreateSectionCommand(string Name, bool? RatesTraits = null) : ICommand<Result<SectionDto>>;

/// <summary>Structural check only — the real length/uniqueness rules live in <see cref="Section.Create"/>.</summary>
internal sealed class CreateSectionCommandValidator : AbstractValidator<CreateSectionCommand>
{
    public CreateSectionCommandValidator() =>
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Section.NameMaxLength);
}
