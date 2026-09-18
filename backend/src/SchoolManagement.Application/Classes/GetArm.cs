using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>GET /api/v1/arms/{id}</c> (spec 6.4.9). Roster, subjects in effect, result-set states and the
/// transfer log (spec 6.4.5) are out of scope — those need pupils, enrolments, subject mappings and
/// results, none of which exist in this codebase yet.
/// </summary>
/// <param name="Id">The arm to read.</param>
public sealed record GetArmQuery(Guid Id) : IQuery<Result<ArmDto>>;

/// <summary>Trivial but mandatory — the query carries no field beyond the route-bound <c>Id</c>.</summary>
internal sealed class GetArmQueryValidator : AbstractValidator<GetArmQuery>;
