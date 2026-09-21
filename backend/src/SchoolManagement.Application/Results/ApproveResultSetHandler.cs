using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="ApproveResultSetCommand"/>.</summary>
/// <remarks>
/// <c>result.approve</c> is the route's ONE declarative privilege (result-set-scoped, like
/// <see cref="SubmitResultSetHandler"/>) — the state check and the <c>needsRecompute</c> guard are
/// DATA-DEPENDENT and so are enforced here, the same "route declares the baseline, handler enforces
/// the data-dependent rest" split <see cref="SubmitResultSetHandler"/> uses. Spec 6.7.11: "Preconditions:
/// none beyond the state" — unlike submit, there is no session/term-closed check here.
/// </remarks>
internal sealed class ApproveResultSetHandler(
    IResultSetRepository resultSets,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<ApproveResultSetCommand, Result<ApproveResultSetResponse>>
{
    /// <summary>Not Awaiting Approval (spec 6.7.11).</summary>
    public const string InvalidStateErrorCode = "result_set.not_awaiting_approval";

    /// <summary>The flag from spec 6.2.9 is set — a settings save since the last computation.</summary>
    public const string NeedsRecomputeErrorCode = "result_set.needs_recompute";

    /// <inheritdoc />
    public async Task<Result<ApproveResultSetResponse>> HandleAsync(ApproveResultSetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // TASK-0088 AC A4/B5's lock rule, reused here: locked BEFORE the state check, so a concurrent
        // settings save that would flag needsRecompute either commits first (and this then sees the
        // flag) or blocks here until this transaction ends.
        var resultSet = await resultSets.FindTrackedByIdForUpdateAsync(request.ResultSetId, cancellationToken).ConfigureAwait(false);
        if (resultSet is null)
        {
            // TASK-0071 ruling, reused here: an unknown result-set id is 403, not 404 — the id is
            // never guessable from a route a legitimate caller would have followed.
            return Result.Failure<ApproveResultSetResponse>(Error.Forbidden(
                "result_set.forbidden", "You do not have access to this result set."));
        }

        if (resultSet.State != ResultSetState.AwaitingApproval)
        {
            return Result.Failure<ApproveResultSetResponse>(Error.Conflict(
                InvalidStateErrorCode, $"This result set is {resultSet.State} and cannot be approved."));
        }

        if (resultSet.NeedsRecompute)
        {
            return Result.Failure<ApproveResultSetResponse>(Error.Conflict(
                NeedsRecomputeErrorCode,
                "Marks have changed since the last computation. Run computation again before approving."));
        }

        var beforeState = resultSet.State;
        var now = timeProvider.GetUtcNow();
        var approvedBy = currentUser.UserId is { } userId ? Guid.Parse(userId) : (Guid?)null;

        var approval = resultSet.Approve(approvedBy, now);
        if (approval.IsFailure)
        {
            return Result.Failure<ApproveResultSetResponse>(approval.Error);
        }

        await auditSink.RecordAsync(
            Privileges.Results.Approve,
            "result_set",
            resultSet.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["state"] = nameof(ResultSetState.Approved),
                ["approvedAt"] = now,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken,
            beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["state"] = beforeState.ToString(),
            }).ConfigureAwait(false);

        var dto = new ResultSetSummaryDto(
            resultSet.Id.ToString("D", CultureInfo.InvariantCulture), resultSet.State, resultSet.NeedsRecompute, resultSet.ReturnReason);

        return Result.Success(new ApproveResultSetResponse(dto, now));
    }
}
