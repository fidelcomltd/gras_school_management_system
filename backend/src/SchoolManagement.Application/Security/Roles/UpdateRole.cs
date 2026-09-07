using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Roles;

/// <summary>
/// <c>PATCH /api/v1/roles/{id}</c> (spec 6.1.4, 6.1.9; approved delta entry <c>TASK-0028</c> §2):
/// "<c>UpdateRoleRequest name?, description?, privileges?, status? (all optional; absent =
/// unchanged)</c>." Every field is independently optional — <see langword="null"/> leaves that field
/// untouched. To clear <see cref="Description"/> to "no description," send an empty string rather
/// than omitting the field: <see langword="null"/> here is indistinguishable from "not provided,"
/// exactly like <c>UpdateAdminAccountCommand.IsSuperAdmin</c>'s own null-means-unchanged convention.
/// </summary>
/// <param name="Id">The role being edited.</param>
/// <param name="Name"><see langword="null"/> to leave unchanged. Rejected if it is the reserved name, case-insensitive.</param>
/// <param name="Description"><see langword="null"/> to leave unchanged; an empty string clears it.</param>
/// <param name="Privileges">
/// <see langword="null"/> to leave unchanged. A non-null value REPLACES the whole set (never a diff) —
/// spec 6.1.7 rule 2 applies only to codes newly present that were not already on the role.
/// </param>
/// <param name="Status"><see langword="null"/> to leave unchanged.</param>
public sealed record UpdateRoleCommand(
    Guid Id,
    string? Name,
    string? Description,
    IReadOnlyList<string>? Privileges,
    RoleStatus? Status)
    : ICommand<Result<RoleDto>>;

/// <summary>Validates <see cref="UpdateRoleCommand"/> against spec 6.1.4's field rules.</summary>
internal sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(Role.NameMaxLength)
            .When(command => command.Name is not null);

        RuleFor(command => command.Description)
            .MaximumLength(Role.DescriptionMaxLength)
            .When(command => command.Description is not null);

        RuleFor(command => command.Privileges)
            .NotEmpty()
            .WithMessage("A role must have at least one privilege.")
            .When(command => command.Privileges is not null);

        RuleFor(command => command.Status).IsInEnum();
    }
}
