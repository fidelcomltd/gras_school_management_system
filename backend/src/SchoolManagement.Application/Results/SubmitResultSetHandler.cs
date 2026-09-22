using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="SubmitResultSetCommand"/>.</summary>
/// <remarks>
/// <c>result.submit</c> is the route's ONE declarative privilege (result-set-scoped, like
/// <c>ComputeResultSet</c>) — every other precondition (state, term/session closure, the completeness
/// gate) is DATA-DEPENDENT and so is enforced here, the same "route declares the baseline, handler
/// enforces the data-dependent rest" split every other data-dependent route in this module uses.
/// </remarks>
internal sealed class SubmitResultSetHandler(
    IArmRepository arms,
    ITermRepository terms,
    IAcademicSessionRepository sessions,
    IResultSetRepository resultSets,
    IResultSetReadinessEvaluator readinessEvaluator,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<SubmitResultSetCommand, Result<SubmitResultSetResponse>>
{
    /// <summary>Not Draft or Returned for Correction (spec 6.7.11).</summary>
    public const string InvalidStateErrorCode = "result_set.invalid_state";

    /// <inheritdoc />
    public async Task<Result<SubmitResultSetResponse>> HandleAsync(SubmitResultSetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // TASK-0088 AC A4/B5: row-locked BEFORE the state check and the readiness re-evaluation below —
        // every sheet save that checks state locks the SAME row (FindTrackedByArmTermForUpdateAsync),
        // so a concurrent save either commits before this does or blocks here until this transaction
        // ends, then sees Awaiting Approval and is refused 409.
        var resultSet = await resultSets.FindTrackedByIdForUpdateAsync(request.ResultSetId, cancellationToken).ConfigureAwait(false);
        if (resultSet is null)
        {
            // TASK-0071 ruling, reused here: an unknown result-set id is 403, not 404 — the id is
            // never guessable from a route a legitimate caller would have followed.
            return Result.Failure<SubmitResultSetResponse>(Error.Forbidden(
                "result_set.forbidden", "You do not have access to this result set."));
        }

        if (resultSet.State is not (ResultSetState.Draft or ResultSetState.ReturnedForCorrection))
        {
            return Result.Failure<SubmitResultSetResponse>(Error.Conflict(
                InvalidStateErrorCode, $"This result set is {resultSet.State} and cannot be submitted."));
        }

        var arm = await arms.FindReadOnlyByIdAsync(resultSet.ArmId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<SubmitResultSetResponse>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(resultSet.TermId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<SubmitResultSetResponse>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var session = await sessions.FindReadOnlyByIdAsync(arm.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is { State: SessionState.Closed })
        {
            return Result.Failure<SubmitResultSetResponse>(Error.Conflict(
                "result_set.session_closed", "This arm's session is closed. The result set cannot be submitted."));
        }

        if (term.State == TermState.Closed)
        {
            return Result.Failure<SubmitResultSetResponse>(Error.Conflict(
                "result_set.term_closed", $"{term.Name} is closed. The result set cannot be submitted."));
        }

        // TASK-0088 AC B5: re-evaluated under the row lock taken above, never off a stale read — and
        // the SAME evaluator GetResultSetReadinessHandler calls, so this 422's body matches the GET's.
        var readiness = await readinessEvaluator.EvaluateAsync(arm, term, resultSet, cancellationToken).ConfigureAwait(false);
        if (!readiness.CanSubmit)
        {
            return Result.Failure<SubmitResultSetResponse>(new ResultSetNotReadyError(readiness));
        }

        var beforeState = resultSet.State;
        var now = timeProvider.GetUtcNow();
        var submittedBy = currentUser.UserId is { } userId ? Guid.Parse(userId) : (Guid?)null;

        resultSet.Submit(submittedBy, now);

        await auditSink.RecordAsync(
            Privileges.Results.Submit,
            "result_set",
            resultSet.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["state"] = nameof(ResultSetState.AwaitingApproval),
                ["submittedAt"] = now,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken,
            beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["state"] = beforeState.ToString(),
            }).ConfigureAwait(false);

        var dto = new ResultSetSummaryDto(
            resultSet.Id.ToString("D", CultureInfo.InvariantCulture), resultSet.State, resultSet.NeedsRecompute, resultSet.ReturnReason);

        return Result.Success(new SubmitResultSetResponse(dto, now));
    }
}
