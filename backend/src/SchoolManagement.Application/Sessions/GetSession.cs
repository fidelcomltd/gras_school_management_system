using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Sessions;

/// <summary><c>GET /api/v1/sessions/{id}</c> (spec 6.3.8, 6.3.10): the session with its three terms.</summary>
/// <param name="Id">The session's identifier.</param>
public sealed record GetSessionQuery(Guid Id) : IQuery<Result<SessionDetailDto>>;

/// <summary>
/// Trivial but mandatory — <c>ValidatorCoverageTests</c> requires one per request even when there is
/// no field to check beyond model binding (an empty route-parameter Guid is a 404, not a 422).
/// </summary>
internal sealed class GetSessionQueryValidator : AbstractValidator<GetSessionQuery>;
