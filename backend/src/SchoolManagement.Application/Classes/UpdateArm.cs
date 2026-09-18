using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>PATCH /api/v1/arms/{id}</c> (spec 6.4.3, 6.4.9): "Label, capacity, form teacher, status." Every
/// field is independently optional — <see langword="null"/> leaves it unchanged, the same convention
/// <c>UpdateLevelCommand</c> established. Rejected outright against a closed arm (spec 6.4.7).
/// </summary>
/// <param name="Id">The arm being edited.</param>
/// <param name="Label"><see langword="null"/> to leave unchanged. Must stay unique within the level and session.</param>
/// <param name="Capacity"><see langword="null"/> to leave unchanged. 1..100.</param>
/// <param name="FormTeacherAdminId">
/// <see langword="null"/> to leave unchanged; an EMPTY string clears it. Setting this ADDITIONALLY
/// requires <c>arm.formteacher.assign</c>, beyond the <c>arm.update</c> this route requires.
/// </param>
/// <param name="Status">
/// <see langword="null"/> to leave unchanged. <c>closed</c> is set automatically when the arm's
/// session closes and cannot be requested here.
/// </param>
public sealed record UpdateArmCommand(
    Guid Id,
    string? Label,
    int? Capacity,
    string? FormTeacherAdminId,
    ArmStatus? Status)
    : ICommand<Result<ArmDto>>;

/// <summary>Structural checks only — uniqueness, admin-account state and the closed-arm rejection live in the handler.</summary>
internal sealed class UpdateArmCommandValidator : AbstractValidator<UpdateArmCommand>
{
    public UpdateArmCommandValidator()
    {
        RuleFor(command => command.Label)
            .NotEmpty()
            .MaximumLength(Arm.LabelMaxLength)
            .When(command => command.Label is not null);

        RuleFor(command => command.Capacity)
            .InclusiveBetween(Arm.MinCapacity, Arm.MaxCapacity)
            .When(command => command.Capacity is not null);

        RuleFor(command => command.FormTeacherAdminId)
            .Must(value => value!.Length == 0 || Guid.TryParse(value, out _))
            .WithMessage("FormTeacherAdminId must be empty (to clear it) or a valid identifier.")
            .When(command => command.FormTeacherAdminId is not null);

        RuleFor(command => command.Status)
            .IsInEnum()
            .NotEqual(ArmStatus.Closed)
            .WithMessage("Status must be active or inactive; closed is set automatically when the session closes.")
            .When(command => command.Status is not null);
    }
}
