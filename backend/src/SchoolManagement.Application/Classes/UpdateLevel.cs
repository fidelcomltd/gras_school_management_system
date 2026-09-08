using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>PATCH /api/v1/levels/{id}</c> (spec 6.4.2, 6.4.9): "Name, section, next level, order, status.
/// Reruns the eight chain rules." Every field is independently optional — <see langword="null"/>
/// leaves it unchanged, the same convention <c>UpdateRoleCommand</c> established.
/// </summary>
/// <param name="Id">The level being edited.</param>
/// <param name="Name"><see langword="null"/> to leave unchanged.</param>
/// <param name="SectionId"><see langword="null"/> to leave unchanged. Must reference an existing section.</param>
/// <param name="NextLevelId">
/// <see langword="null"/> to leave unchanged; an EMPTY string clears it (the level becomes a
/// graduating candidate) — <see langword="null"/> here is indistinguishable from "not provided,"
/// exactly like <c>UpdateRoleCommand.Description</c>'s null-means-unchanged convention.
/// </param>
/// <param name="ProgressionOrder"><see langword="null"/> to leave unchanged.</param>
/// <param name="Status">
/// <see langword="null"/> to leave unchanged. Changing this ALSO requires <c>level.deactivate</c>
/// (checked in the handler, data-dependent — the route itself requires only <c>level.update</c>).
/// </param>
public sealed record UpdateLevelCommand(
    Guid Id,
    string? Name,
    string? SectionId,
    string? NextLevelId,
    int? ProgressionOrder,
    LevelStatus? Status)
    : ICommand<Result<LevelDto>>;

/// <summary>Structural checks only — the real chain rules live in <see cref="ProgressionChainGuard"/>.</summary>
internal sealed class UpdateLevelCommandValidator : AbstractValidator<UpdateLevelCommand>
{
    public UpdateLevelCommandValidator()
    {
        RuleFor(command => command.Name)
            .MinimumLength(ClassLevel.NameMinLength)
            .MaximumLength(ClassLevel.NameMaxLength)
            .When(command => command.Name is not null);

        RuleFor(command => command.SectionId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("SectionId must be a valid identifier.")
            .When(command => command.SectionId is not null);

        RuleFor(command => command.NextLevelId)
            .Must(value => value!.Length == 0 || Guid.TryParse(value, out _))
            .WithMessage("NextLevelId must be empty (to clear it) or a valid identifier.")
            .When(command => command.NextLevelId is not null);

        RuleFor(command => command.ProgressionOrder)
            .GreaterThanOrEqualTo(1)
            .When(command => command.ProgressionOrder is not null);

        RuleFor(command => command.Status).IsInEnum();
    }
}
