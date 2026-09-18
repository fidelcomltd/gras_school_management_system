using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Security.Assignments;

/// <summary>
/// <c>GET /api/v1/admins/{id}/assignments</c> (spec 6.1.5, 6.1.14): "The account's assignments with
/// role, scope type, scope ids, session." Returns every assignment (active and revoked) — small,
/// admin-configuration-sized per account, so this is a plain array rather than cursor-paginated.
/// </summary>
/// <param name="AdminAccountId">The account whose assignments to list — bound from the route.</param>
public sealed record ListRoleAssignmentsQuery(string AdminAccountId)
    : IQuery<Result<IReadOnlyList<RoleAssignmentDto>>>;

/// <summary>Structural checks only.</summary>
internal sealed class ListRoleAssignmentsQueryValidator : AbstractValidator<ListRoleAssignmentsQuery>
{
    public ListRoleAssignmentsQueryValidator()
    {
        RuleFor(query => query.AdminAccountId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("AdminAccountId must be a valid identifier.");
    }
}
