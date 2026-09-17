using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Sessions;

/// <summary>
/// Handles <see cref="CloseTermCommand"/>.
/// </summary>
/// <remarks>
/// TASK-0076 dispatch A wires spec 6.3.6's result-set precondition, previously DEFERRED because
/// <c>result_set</c> did not exist (see STATE.md's now-struck drift entry). Checked BEFORE
/// <see cref="SchoolManagement.Domain.Sessions.Term.Close"/> is even called, so a blocked close
/// never mutates the tracked term at all. <see cref="SchoolManagement.Domain.Sessions.Term.Close"/>
/// still separately enforces <c>times_school_opened</c> set.
/// </remarks>
internal sealed class CloseTermHandler(
    ITermRepository terms,
    IResultSetRepository resultSets,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<CloseTermCommand, Result<TermDto>>
{
    /// <summary>Stable error code for spec 6.3.6's result-set precondition.</summary>
    public const string BlockedByResultSetsErrorCode = "term.close_blocked_by_result_sets";

    /// <inheritdoc />
    public async Task<Result<TermDto>> HandleAsync(CloseTermCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var term = await terms.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (term is null)
        {
            return Result.Failure<TermDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var blocking = await resultSets.ListBlockingTermCloseAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (blocking.Count > 0)
        {
            var offendingArms = string.Join(
                ", ",
                blocking.Select(summary => $"{summary.ArmDisplayName} ({DescribeState(summary.State)})"));

            return Result.Failure<TermDto>(Error.Conflict(
                BlockedByResultSetsErrorCode,
                $"These arms have results that are not published: {offendingArms}. Publish or withdraw them before closing the term."));
        }

        var close = term.Close(timeProvider.GetUtcNow(), currentUser.UserId);

        if (close.IsFailure)
        {
            return Result.Failure<TermDto>(close.Error);
        }

        await auditSink.RecordAsync(
            Privileges.Term.Close,
            "term",
            term.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SessionMapper.ToTermDto(term));
    }

    /// <summary>
    /// Humanises a <see cref="ResultSetState"/> for spec 6.3.6's message, which prints "Awaiting
    /// Approval" and "Returned for Correction" with spaces — the wire/enum spelling (PascalCase, no
    /// spaces) is a separate concern from this one rejection message's wording.
    /// </summary>
    private static string DescribeState(ResultSetState state) => state switch
    {
        ResultSetState.AwaitingApproval => "Awaiting Approval",
        ResultSetState.ReturnedForCorrection => "Returned for Correction",
        _ => state.ToString(),
    };
}
