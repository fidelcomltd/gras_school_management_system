using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Assignments;

/// <summary>
/// <c>POST /api/v1/admins/{id}/assignments</c> (spec 6.1.5). Requires <c>role.assign</c> for a
/// school-wide grant or <c>role.scope.assign</c> for an arm-scoped grant — data-dependent on
/// <see cref="ScopeType"/>, so the handler resolves and enforces it rather than a route-declarative
/// privilege. Subject to escalation rules 1 and 3 (spec 6.1.7).
/// </summary>
/// <param name="AdminAccountId">The account receiving the role — bound from the route, not the body.</param>
/// <param name="RoleId">Must reference an active role, not the seeded Super Admin role.</param>
/// <param name="SessionId">Must reference an existing session. Every arm in <see cref="ArmIds"/> must belong to it.</param>
/// <param name="ScopeType">School-wide or arm-list.</param>
/// <param name="ArmIds">Required and non-empty when <see cref="ScopeType"/> is arm-list; otherwise must be empty or omitted.</param>
public sealed record CreateRoleAssignmentCommand(
    string AdminAccountId,
    string RoleId,
    string SessionId,
    ScopeType ScopeType,
    IReadOnlyList<string>? ArmIds)
    : ICommand<Result<RoleAssignmentDto>>;

/// <summary>Structural checks only — account/role/session existence and the escalation rules live in the handler.</summary>
internal sealed class CreateRoleAssignmentCommandValidator : AbstractValidator<CreateRoleAssignmentCommand>
{
    public CreateRoleAssignmentCommandValidator()
    {
        RuleFor(command => command.AdminAccountId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("AdminAccountId must be a valid identifier.");

        RuleFor(command => command.RoleId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("RoleId must be a valid identifier.");

        RuleFor(command => command.SessionId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("SessionId must be a valid identifier.");

        RuleFor(command => command.ScopeType)
            .IsInEnum();

        RuleForEach(command => command.ArmIds)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("Every arm id must be a valid identifier.")
            .When(command => command.ArmIds is not null);

        RuleFor(command => command.ArmIds)
            .Must(armIds => armIds is { Count: > 0 })
            .WithMessage("At least one arm is required for an arm-scoped assignment.")
            .When(command => command.ScopeType == ScopeType.ArmList);

        RuleFor(command => command.ArmIds)
            .Must(armIds => armIds is null or { Count: 0 })
            .WithMessage("A school-wide assignment must not name any arms.")
            .When(command => command.ScopeType == ScopeType.SchoolWide);
    }
}
