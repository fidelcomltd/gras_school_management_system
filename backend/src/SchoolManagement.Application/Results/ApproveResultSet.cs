using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>POST /api/v1/result-sets/{resultSetId}/approve</c> (spec 6.7.8, 6.7.11; TASK-0090's approved
/// contract delta). No body — the route's <c>resultSetId</c> is the whole input, like
/// <see cref="SubmitResultSetCommand"/>. Moves Awaiting Approval to Approved.
/// </summary>
/// <param name="ResultSetId">The result set to approve, from the route.</param>
public sealed record ApproveResultSetCommand(Guid ResultSetId) : ICommand<Result<ApproveResultSetResponse>>;

/// <summary>Trivial but mandatory — the command carries no field beyond the route-bound <c>ResultSetId</c>.</summary>
internal sealed class ApproveResultSetCommandValidator : AbstractValidator<ApproveResultSetCommand>;

/// <summary>The 200 response (contract delta item 1).</summary>
/// <param name="ResultSet">The set's new state (Approved) and its other summary fields.</param>
/// <param name="ApprovedAt">When this approval was recorded.</param>
public sealed record ApproveResultSetResponse(ResultSetSummaryDto ResultSet, DateTimeOffset ApprovedAt);
