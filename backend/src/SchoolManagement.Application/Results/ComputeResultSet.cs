using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>POST /api/v1/result-sets/{resultSetId}/compute</c> (spec 8.2; TASK-0071's approved contract
/// delta). No body — the route's <c>resultSetId</c> is the whole input. Idempotent by definition:
/// running it twice on unchanged inputs produces identical computed rows (spec 6.7.6).
/// </summary>
/// <param name="ResultSetId">The result set to compute, from the route.</param>
public sealed record ComputeResultSetCommand(Guid ResultSetId) : ICommand<Result<ComputeResultSetResponse>>;

/// <summary>Trivial but mandatory — the command carries no field beyond the route-bound <c>ResultSetId</c>.</summary>
internal sealed class ComputeResultSetCommandValidator : AbstractValidator<ComputeResultSetCommand>;

/// <summary>One computation flag (contract delta's <c>flags[]</c>) — response-body data, NOT a problem code, so its <see cref="Code"/> is not dotted.</summary>
/// <param name="Code"><c>no_examination_sat</c> or <c>absent_all_examinations</c>.</param>
/// <param name="SubjectId">Set for <c>no_examination_sat</c>.</param>
/// <param name="PupilId">Set for <c>absent_all_examinations</c>.</param>
public sealed record ComputeResultSetFlagDto(string Code, string? SubjectId, string? PupilId);

/// <summary>The 200 response (contract delta).</summary>
/// <param name="ResultSetId">The result set that was computed.</param>
/// <param name="ComputedAt">When this computation ran.</param>
/// <param name="PupilCount">Pupils ranked (eligible for arm position) — the denominator the sheet prints.</param>
/// <param name="SubjectCount">Subjects in effect for the arm this term.</param>
/// <param name="Flags">Spec 6.7.12/6.7.4's computation-time flags.</param>
public sealed record ComputeResultSetResponse(
    string ResultSetId, DateTimeOffset ComputedAt, int PupilCount, int SubjectCount, IReadOnlyList<ComputeResultSetFlagDto> Flags);
