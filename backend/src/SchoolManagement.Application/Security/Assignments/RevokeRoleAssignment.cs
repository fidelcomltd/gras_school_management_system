using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Security.Assignments;

/// <summary>
/// <c>DELETE /api/v1/assignments/{id}</c> (spec 6.1.14). Requires <c>role.assign</c> — a fixed
/// privilege per the approved contract delta, so this route is gated declaratively; the handler
/// separately enforces escalation rule 1 (spec 6.1.7), which is data-dependent on the assignment's
/// own <c>admin_account_id</c>.
/// </summary>
/// <param name="Id">The assignment to revoke — bound from the route.</param>
public sealed record RevokeRoleAssignmentCommand(string Id) : ICommand<Result>;

/// <summary>Structural checks only.</summary>
internal sealed class RevokeRoleAssignmentCommandValidator : AbstractValidator<RevokeRoleAssignmentCommand>
{
    public RevokeRoleAssignmentCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("Id must be a valid identifier.");
    }
}
