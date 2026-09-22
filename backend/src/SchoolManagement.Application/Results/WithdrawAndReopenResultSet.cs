using System.Globalization;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Results;

/// <summary>The request body for a withdrawal.</summary>
/// <param name="Reason">Why the published result is being pulled, 10 to 500 characters once trimmed. Audited.</param>
public sealed record WithdrawResultSetRequest(string Reason);

/// <summary>Withdraws a published result set from the parent portal (spec 6.7.9).</summary>
/// <param name="ResultSetId">From the route.</param>
/// <param name="Reason">From the body.</param>
public sealed record WithdrawResultSetCommand(Guid ResultSetId, string Reason) : ICommand<Result<ResultSetTransitionResponse>>;

/// <summary>Reopens a withdrawn result set for correction (spec 6.7.9, 6.7.11).</summary>
/// <param name="ResultSetId">From the route.</param>
public sealed record ReopenResultSetCommand(Guid ResultSetId) : ICommand<Result<ResultSetTransitionResponse>>;

/// <summary>The result set after a withdraw or reopen.</summary>
/// <param name="ResultSet">Its new state.</param>
public sealed record ResultSetTransitionResponse(ResultSetSummaryDto ResultSet);

/// <summary>Same bounds as a return reason (spec 6.7.8, 6.7.9: "reason of at least ten characters").</summary>
internal sealed class WithdrawResultSetCommandValidator : AbstractValidator<WithdrawResultSetCommand>
{
    public WithdrawResultSetCommandValidator() =>
        RuleFor(command => command.Reason)
            .NotNull()
            .Must(reason => reason.Trim().Length is >= ResultSet.ReturnReasonMinLength and <= ResultSet.ReturnReasonMaxLength)
            .WithMessage($"Reason must be between {ResultSet.ReturnReasonMinLength} and {ResultSet.ReturnReasonMaxLength} characters.");
}

/// <summary>No body; the route supplies the id.</summary>
internal sealed class ReopenResultSetCommandValidator : AbstractValidator<ReopenResultSetCommand>;

/// <summary>Handles <see cref="WithdrawResultSetCommand"/>. The snapshot and its history row are kept.</summary>
internal sealed class WithdrawResultSetHandler(
    IResultSetRepository resultSets,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<WithdrawResultSetCommand, Result<ResultSetTransitionResponse>>
{
    /// <inheritdoc />
    public async Task<Result<ResultSetTransitionResponse>> HandleAsync(WithdrawResultSetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resultSet = await resultSets.FindTrackedByIdForUpdateAsync(request.ResultSetId, cancellationToken).ConfigureAwait(false);
        if (resultSet is null)
        {
            return Result.Failure<ResultSetTransitionResponse>(Error.NotFound("result_set.not_found", "No result set was found with that id."));
        }

        var withdrawal = resultSet.Withdraw();
        if (withdrawal.IsFailure)
        {
            return Result.Failure<ResultSetTransitionResponse>(withdrawal.Error);
        }

        await ResultSetTransitionAudit.RecordAsync(
            auditSink, currentUser, Privileges.Results.Unpublish, resultSet, ResultSetState.Published,
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["reason"] = request.Reason.Trim() },
            cancellationToken).ConfigureAwait(false);

        return Result.Success(ResultSetTransitionAudit.Respond(resultSet));
    }
}

/// <summary>Handles <see cref="ReopenResultSetCommand"/>. The term must be active (spec 6.7.9: "term to be active or reopened").</summary>
internal sealed class ReopenResultSetHandler(
    IResultSetRepository resultSets,
    ITermRepository terms,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<ReopenResultSetCommand, Result<ResultSetTransitionResponse>>
{
    /// <inheritdoc />
    public async Task<Result<ResultSetTransitionResponse>> HandleAsync(ReopenResultSetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resultSet = await resultSets.FindTrackedByIdForUpdateAsync(request.ResultSetId, cancellationToken).ConfigureAwait(false);
        if (resultSet is null)
        {
            return Result.Failure<ResultSetTransitionResponse>(Error.NotFound("result_set.not_found", "No result set was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(resultSet.TermId, cancellationToken).ConfigureAwait(false);
        if (term is not { State: TermState.Active })
        {
            return Result.Failure<ResultSetTransitionResponse>(Error.Conflict(
                "result_set.term_not_active", "The term is not active. Reopen the term before reopening its results for correction."));
        }

        var reopening = resultSet.Reopen();
        if (reopening.IsFailure)
        {
            return Result.Failure<ResultSetTransitionResponse>(reopening.Error);
        }

        await ResultSetTransitionAudit.RecordAsync(
            auditSink, currentUser, "result.reopen", resultSet, ResultSetState.Withdrawn, extra: null, cancellationToken).ConfigureAwait(false);

        return Result.Success(ResultSetTransitionAudit.Respond(resultSet));
    }
}

/// <summary>The audit event and response shape both transitions share.</summary>
internal static class ResultSetTransitionAudit
{
    public static Task RecordAsync(
        ISystemAuditSink auditSink,
        ICurrentUser currentUser,
        string action,
        ResultSet resultSet,
        ResultSetState beforeState,
        Dictionary<string, object?>? extra,
        CancellationToken cancellationToken)
    {
        var after = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["state"] = resultSet.State.ToString(),
            ["needsRecompute"] = resultSet.NeedsRecompute,
            ["revisionNumber"] = resultSet.RevisionNumber,
        };
        foreach (var (key, value) in extra ?? [])
        {
            after[key] = value;
        }

        return auditSink.RecordAsync(
            action,
            "result_set",
            resultSet.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: after,
            actorAdminId: currentUser.UserId,
            cancellationToken,
            beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["state"] = beforeState.ToString() });
    }

    public static ResultSetTransitionResponse Respond(ResultSet resultSet) =>
        new(new ResultSetSummaryDto(
            resultSet.Id.ToString("D", CultureInfo.InvariantCulture), resultSet.State, resultSet.NeedsRecompute, resultSet.ReturnReason));
}
