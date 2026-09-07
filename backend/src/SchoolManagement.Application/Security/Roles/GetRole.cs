using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Security.Roles;

/// <summary><c>GET /api/v1/roles/{id}</c> (approved delta entry <c>TASK-0028</c> §2).</summary>
/// <param name="Id">The role's identifier.</param>
public sealed record GetRoleQuery(Guid Id) : IQuery<Result<RoleDto>>;

/// <summary>
/// Trivial but mandatory — <c>ValidatorCoverageTests</c> requires one per request even when there is
/// no field to check beyond model binding (an empty route-parameter Guid is a 404, not a 422).
/// </summary>
internal sealed class GetRoleQueryValidator : AbstractValidator<GetRoleQuery>;
