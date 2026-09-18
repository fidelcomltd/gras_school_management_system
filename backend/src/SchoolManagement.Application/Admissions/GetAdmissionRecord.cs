using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Admissions;

/// <summary>
/// <c>GET /api/v1/admissions/{id}</c> (TASK-0066). <paramref name="Id"/> is the PUPIL id — the same
/// identity <c>PATCH /admissions/{id}</c> and the admissions queue already use.
/// </summary>
/// <param name="Id">The pupil whose admission record is being read. Supplied from the route.</param>
public sealed record GetAdmissionRecordQuery(Guid Id) : IQuery<Result<AdmissionRecordDto>>;

/// <summary>Trivial but mandatory — the query carries no field beyond the route-bound <c>Id</c>.</summary>
internal sealed class GetAdmissionRecordQueryValidator : AbstractValidator<GetAdmissionRecordQuery>;
