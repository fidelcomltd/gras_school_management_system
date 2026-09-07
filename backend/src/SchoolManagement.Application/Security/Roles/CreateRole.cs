using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Roles;

/// <summary>
/// <c>POST /api/v1/roles</c> (spec 6.1.4; approved delta
/// <c>.agent/decisions/2026-Q3-contract-deltas.md</c> entry <c>TASK-0028</c> §2). Rejects the reserved
/// name <c>Super Admin</c>, case-insensitive, and an unknown privilege code naming the offender.
/// Spec 6.1.7 rule 2 applies to every requested privilege (the role starts with none, so every one
/// requested is an "add") — enforced by the handler, not this type.
/// </summary>
/// <param name="Name">1..60 characters.</param>
/// <param name="Description">0..300 characters, or <see langword="null"/>.</param>
/// <param name="Privileges">
/// At least one privilege code. Legacy <c>guardian.*</c> aliases are accepted and resolved to their
/// canonical replacement before storage.
/// </param>
public sealed record CreateRoleCommand(string Name, string? Description, IReadOnlyList<string> Privileges)
    : ICommand<Result<RoleDto>>;

/// <summary>Validates <see cref="CreateRoleCommand"/> against spec 6.1.4's field rules.</summary>
internal sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(Role.NameMaxLength);

        RuleFor(command => command.Description)
            .MaximumLength(Role.DescriptionMaxLength)
            .When(command => command.Description is not null);

        RuleFor(command => command.Privileges)
            .NotEmpty()
            .WithMessage("A role must have at least one privilege.");
    }
}
