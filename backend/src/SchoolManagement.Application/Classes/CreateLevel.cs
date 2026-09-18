using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>POST /api/v1/levels</c> (spec 6.4.2, 6.4.9). Two mutually exclusive ways to place the new level
/// in the chain: give <see cref="InsertAfterLevelId"/> and the server rewires everything (spec 6.4.2's
/// worked case), OR give <see cref="ProgressionOrder"/> directly (spec 6.4.2: "editable directly for
/// the administrator who prefers typing") with an optional <see cref="NextLevelId"/>.
/// </summary>
/// <param name="Name">2..40 characters, trimmed. Unique, case-insensitive.</param>
/// <param name="SectionId">Must reference an existing section.</param>
/// <param name="ProgressionOrder">Required when <see cref="InsertAfterLevelId"/> is absent; must be omitted when it is present.</param>
/// <param name="NextLevelId"><see langword="null"/> for a graduating candidate. Must be omitted when <see cref="InsertAfterLevelId"/> is present.</param>
/// <param name="InsertAfterLevelId">
/// When given, must reference an active level; the server sets order and both neighbours'
/// <c>nextLevelId</c> in one transaction, shifting every later active level's order up by one.
/// </param>
public sealed record CreateLevelCommand(
    string Name,
    string SectionId,
    int? ProgressionOrder,
    string? NextLevelId,
    string? InsertAfterLevelId)
    : ICommand<Result<LevelDto>>;

/// <summary>Structural checks only — the real chain rules live in <see cref="ProgressionChainGuard"/>.</summary>
internal sealed class CreateLevelCommandValidator : AbstractValidator<CreateLevelCommand>
{
    public CreateLevelCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MinimumLength(ClassLevel.NameMinLength)
            .MaximumLength(ClassLevel.NameMaxLength);

        RuleFor(command => command.SectionId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("SectionId must be a valid identifier.");

        RuleFor(command => command.NextLevelId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("NextLevelId must be a valid identifier.")
            .When(command => command.NextLevelId is not null);

        RuleFor(command => command.InsertAfterLevelId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("InsertAfterLevelId must be a valid identifier.")
            .When(command => command.InsertAfterLevelId is not null);

        RuleFor(command => command.ProgressionOrder)
            .NotNull()
            .WithMessage("ProgressionOrder is required unless insertAfterLevelId is given.")
            .When(command => command.InsertAfterLevelId is null);

        RuleFor(command => command.ProgressionOrder)
            .Null()
            .WithMessage("ProgressionOrder must be omitted when insertAfterLevelId is given.")
            .When(command => command.InsertAfterLevelId is not null);

        RuleFor(command => command.NextLevelId)
            .Null()
            .WithMessage("NextLevelId must be omitted when insertAfterLevelId is given.")
            .When(command => command.InsertAfterLevelId is not null);

        RuleFor(command => command.ProgressionOrder)
            .GreaterThanOrEqualTo(1)
            .When(command => command.ProgressionOrder is not null);
    }
}
