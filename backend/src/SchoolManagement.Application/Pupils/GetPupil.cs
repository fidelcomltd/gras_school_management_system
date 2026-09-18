using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// <c>GET /api/v1/pupils/{id}</c> (spec 6.5.15). Renders only what exists after this card — no
/// contacts/health/documents/enrolment-history blocks yet (later cards).
/// </summary>
public sealed record GetPupilQuery(Guid Id) : IQuery<Result<PupilDto>>;

/// <summary>Trivial but mandatory — the query carries no field beyond the route-bound <c>Id</c>.</summary>
internal sealed class GetPupilQueryValidator : AbstractValidator<GetPupilQuery>;
