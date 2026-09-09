using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>POST /api/v1/arms</c> (spec 6.4.3, 6.4.9). Single-arm creation — the flow spec 6.4.4 exists for:
/// opening a new room mid-term under an active level, permitted with no privilege beyond
/// <c>arm.create</c>.
/// </summary>
/// <param name="ClassLevelId">Must reference an active level.</param>
/// <param name="SessionId">Must reference an upcoming or active session.</param>
/// <param name="Label">1..16 characters, letters/digits/single internal spaces. Unique within the level and session, case-insensitive.</param>
/// <param name="Capacity"><see langword="null"/> defaults to <see cref="Arm.DefaultCapacity"/>. 1..100.</param>
/// <param name="FormTeacherAdminId">Optional. Must reference an active admin account when given.</param>
public sealed record CreateArmCommand(
    string ClassLevelId,
    string SessionId,
    string Label,
    int? Capacity,
    string? FormTeacherAdminId)
    : ICommand<Result<ArmDto>>;

/// <summary>Structural checks only — level/session/admin existence and their state live in the handler.</summary>
internal sealed class CreateArmCommandValidator : AbstractValidator<CreateArmCommand>
{
    public CreateArmCommandValidator()
    {
        RuleFor(command => command.ClassLevelId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("ClassLevelId must be a valid identifier.");

        RuleFor(command => command.SessionId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("SessionId must be a valid identifier.");

        RuleFor(command => command.Label)
            .NotEmpty()
            .MaximumLength(Arm.LabelMaxLength);

        RuleFor(command => command.Capacity)
            .InclusiveBetween(Arm.MinCapacity, Arm.MaxCapacity)
            .When(command => command.Capacity is not null);

        RuleFor(command => command.FormTeacherAdminId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("FormTeacherAdminId must be a valid identifier.")
            .When(command => command.FormTeacherAdminId is not null);
    }
}
