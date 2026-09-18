using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// <c>PATCH /api/v1/subjects/{id}</c> (spec 6.6.2, 6.6.9). Every field is independently optional —
/// <see langword="null"/> leaves it unchanged, the same convention <c>UpdateArmCommand</c> and
/// <c>UpdateLevelCommand</c> established. Changing <see cref="Status"/> ADDITIONALLY requires
/// <c>subject.deactivate</c>, beyond the <c>subject.update</c> this route requires.
/// </summary>
/// <param name="Id">The subject being edited.</param>
/// <param name="Name"><see langword="null"/> to leave unchanged. Must stay unique.</param>
/// <param name="Code"><see langword="null"/> to leave unchanged; an EMPTY string clears it.</param>
/// <param name="Description"><see langword="null"/> to leave unchanged; an EMPTY string clears it.</param>
/// <param name="Status"><see langword="null"/> to leave unchanged.</param>
public sealed record UpdateSubjectCommand(
    Guid Id,
    string? Name,
    string? Code,
    string? Description,
    SubjectStatus? Status)
    : ICommand<Result<SubjectDto>>;

/// <summary>Structural checks only — uniqueness lives in the handler.</summary>
internal sealed class UpdateSubjectCommandValidator : AbstractValidator<UpdateSubjectCommand>
{
    public UpdateSubjectCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(Subject.NameMaxLength)
            .When(command => command.Name is not null);

        RuleFor(command => command.Code)
            .MaximumLength(Subject.CodeMaxLength)
            .Matches("^[A-Z0-9]+$")
            .WithMessage("Code may contain only uppercase letters and digits.")
            .When(command => !string.IsNullOrEmpty(command.Code));

        RuleFor(command => command.Description)
            .MaximumLength(Subject.DescriptionMaxLength)
            .When(command => !string.IsNullOrEmpty(command.Description));

        RuleFor(command => command.Status).IsInEnum().When(command => command.Status is not null);
    }
}
