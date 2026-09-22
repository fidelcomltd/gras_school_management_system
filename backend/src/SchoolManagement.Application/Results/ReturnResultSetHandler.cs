using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="ReturnResultSetCommand"/>.</summary>
/// <remarks>
/// <c>result.return</c> is the route's ONE declarative privilege — SCHOOL-WIDE, not result-set-scoped
/// (coordinator amendment, TASK-0090 review: same separation reasoning as
/// <see cref="ApproveResultSetHandler"/>). The state check is DATA-DEPENDENT and so is enforced here.
/// A set can be returned any number of times, from Awaiting Approval or from Approved (spec 6.7.8);
/// each return is its OWN audit event, even when it overwrites a still-live reason from an earlier one.
/// </remarks>
internal sealed class ReturnResultSetHandler(
    IResultSetRepository resultSets,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<ReturnResultSetCommand, Result<ReturnResultSetResponse>>
{
    /// <summary>Neither Awaiting Approval nor Approved (spec 6.7.11).</summary>
    public const string InvalidStateErrorCode = "result_set.not_returnable";

    /// <inheritdoc />
    public async Task<Result<ReturnResultSetResponse>> HandleAsync(ReturnResultSetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // TASK-0088 AC A4/B5's lock rule, reused here.
        var resultSet = await resultSets.FindTrackedByIdForUpdateAsync(request.ResultSetId, cancellationToken).ConfigureAwait(false);
        if (resultSet is null)
        {
            // Coordinator amendment, TASK-0090 review: this route is school-wide (unscoped), so the
            // TASK-0071 403-for-unknown-id ruling (which applies only to a SCOPED route) does not
            // apply. Plain 404, same as ComputeResultSetHandler.
            return Result.Failure<ReturnResultSetResponse>(Error.NotFound(
                "result_set.not_found", "No result set was found with that id."));
        }

        if (resultSet.State is not (ResultSetState.AwaitingApproval or ResultSetState.Approved))
        {
            return Result.Failure<ReturnResultSetResponse>(Error.Conflict(
                InvalidStateErrorCode, $"This result set is {resultSet.State} and cannot be returned."));
        }

        var beforeState = resultSet.State;
        var beforeReason = resultSet.ReturnReason;
        var reason = request.Reason.Trim();

        var returned = resultSet.Return(reason);
        if (returned.IsFailure)
        {
            return Result.Failure<ReturnResultSetResponse>(returned.Error);
        }

        // Spec 6.7.8: "each return is a separate audit event" — recorded even when it overwrites a
        // still-live reason from an earlier return, which is why before_json carries the PRIOR reason
        // rather than assuming it was null.
        await auditSink.RecordAsync(
            Privileges.Results.Return,
            "result_set",
            resultSet.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["state"] = nameof(ResultSetState.ReturnedForCorrection),
                ["reason"] = reason,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken,
            beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["state"] = beforeState.ToString(),
                ["reason"] = beforeReason,
            }).ConfigureAwait(false);

        var dto = new ResultSetSummaryDto(
            resultSet.Id.ToString("D", CultureInfo.InvariantCulture), resultSet.State, resultSet.NeedsRecompute, resultSet.ReturnReason);

        return Result.Success(new ReturnResultSetResponse(dto));
    }
}
