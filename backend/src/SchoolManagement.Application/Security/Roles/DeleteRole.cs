using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;

namespace SchoolManagement.Application.Security.Roles;

/// <summary>
/// <c>DELETE /api/v1/roles/{id}</c> (spec 9.4; approved delta entry <c>TASK-0028</c> §4). Hard-deletes
/// unconditionally except on a system role (409) — §9.4 permits this "when no assignment has ever
/// used it," and TASK-0028 has no <c>role_assignment</c> table at all, so nothing can ever have
/// referenced a role yet. TASK-0030 must add the has-ever-been-assigned branch when that table exists
/// (STATE.md live drift).
/// </summary>
/// <param name="Id">The role being removed.</param>
public sealed record DeleteRoleCommand(Guid Id) : ICommand;

/// <summary>
/// Trivial but mandatory — the command carries no field beyond the route-bound <c>Id</c>.
/// </summary>
internal sealed class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>;
