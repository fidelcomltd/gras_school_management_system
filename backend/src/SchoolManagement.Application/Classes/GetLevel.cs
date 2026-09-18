using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>GET /api/v1/levels/{id}</c> (spec 6.4.9). No arm counts — that needs <c>Arm</c>, TASK-0039.
/// </summary>
/// <param name="Id">The level to read.</param>
public sealed record GetLevelQuery(Guid Id) : IQuery<Result<LevelDto>>;

/// <summary>Trivial but mandatory — the query carries no field beyond the route-bound <c>Id</c>.</summary>
internal sealed class GetLevelQueryValidator : AbstractValidator<GetLevelQuery>;
