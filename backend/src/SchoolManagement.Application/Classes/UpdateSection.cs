using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary><c>PATCH /api/v1/sections/{id}</c> (spec 6.4.9). Name only.</summary>
/// <param name="Id">The section being renamed.</param>
/// <param name="Name">2..40 characters, trimmed. Unique, case-insensitive.</param>
public sealed record UpdateSectionCommand(Guid Id, string Name) : ICommand<Result<SectionDto>>;

/// <summary>Structural check only — the real length rule lives in <see cref="Section.Rename"/>.</summary>
internal sealed class UpdateSectionCommandValidator : AbstractValidator<UpdateSectionCommand>
{
    public UpdateSectionCommandValidator() =>
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Section.NameMaxLength);
}
