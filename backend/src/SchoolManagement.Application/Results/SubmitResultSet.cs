using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>POST /api/v1/result-sets/{resultSetId}/submit</c> (spec 6.7.5, 6.7.11; TASK-0088 stage B's
/// approved contract delta). No body — the route's <c>resultSetId</c> is the whole input, like
/// <see cref="ComputeResultSetCommand"/>. Moves Draft or Returned for Correction to Awaiting Approval.
/// </summary>
/// <param name="ResultSetId">The result set to submit, from the route.</param>
public sealed record SubmitResultSetCommand(Guid ResultSetId) : ICommand<Result<SubmitResultSetResponse>>;

/// <summary>Trivial but mandatory — the command carries no field beyond the route-bound <c>ResultSetId</c>.</summary>
internal sealed class SubmitResultSetCommandValidator : AbstractValidator<SubmitResultSetCommand>;

/// <summary>The 200 response (contract delta item 2).</summary>
/// <param name="ResultSet">The set's new state (Awaiting Approval) and its other summary fields.</param>
/// <param name="SubmittedAt">When this submission was recorded.</param>
public sealed record SubmitResultSetResponse(ResultSetSummaryDto ResultSet, DateTimeOffset SubmittedAt);

/// <summary>
/// The 422 failure (contract delta item 2) — the completeness gate refused submission. Carries the
/// SAME <see cref="ResultSetReadinessDto"/> <c>GET /arms/{armId}/readiness</c> returns for this set
/// (AC B6), so the screen needs no second call to show what is still missing (spec 6.7.5: "Attempting
/// submission by endpoint returns 422 with a structured list of what is missing"). The contract's
/// FIRST typed problem-details extension — see <c>ResultSetNotReadyProblemDetails</c>, the Api-layer
/// shape declared purely for the OpenAPI document (TASK-0049's trap: a type-less <c>Produces</c>
/// silently drops <c>content</c>).
/// </summary>
/// <param name="Readiness">The readiness snapshot that failed the gate.</param>
public sealed record ResultSetNotReadyError(ResultSetReadinessDto Readiness)
    : Error(ErrorCode, "This result set is not ready to submit. See the readiness grid.", ErrorType.Validation)
{
    /// <summary>Stable error code for this failure.</summary>
    public const string ErrorCode = "result_set.not_ready";
}
