using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>
/// <c>GET /api/v1/admins/{id}</c> (spec 6.1.8). Assignments, effective privileges and the last ten
/// audit events are TASK-0028 (approved delta, entry `TASK-0019/0027`, B4) — this card returns the
/// account's own fields only.
/// </summary>
/// <param name="Id">The account's identifier.</param>
public sealed record GetAdminAccountQuery(Guid Id) : IQuery<Result<AdminAccountDetailDto>>;

/// <summary>
/// Trivial but mandatory — <c>ValidatorCoverageTests</c> requires one per request even when there is
/// no field to check beyond model binding (an empty route-parameter Guid is a 404, not a 422).
/// </summary>
internal sealed class GetAdminAccountQueryValidator : AbstractValidator<GetAdminAccountQuery>;
