using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Sessions;

/// <summary>
/// Handles <see cref="CloseTermCommand"/>.
/// </summary>
/// <remarks>
/// DEFERRED (spec 6.3.6, no task card yet): closing is blocked by any result set in the term that is
/// Draft, Awaiting Approval or Approved with marks entered, naming the offending arms. Result sets
/// are spec 09 §6.7 and do not exist anywhere in this codebase yet, so that precondition is not
/// checked below — this makes <c>close</c> MORE PERMISSIVE than spec until that card lands.
/// Deliberate and visible, not a silent omission: see TASK-0035's Log and STATE.md
/// <c>## Known drift</c> for the tracked entry. <see cref="SchoolManagement.Domain.Sessions.Term.Close"/>
/// still enforces the one precondition this card CAN check honestly — <c>times_school_opened</c> set.
/// </remarks>
internal sealed class CloseTermHandler(
    ITermRepository terms,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<CloseTermCommand, Result<TermDto>>
{
    /// <inheritdoc />
    public async Task<Result<TermDto>> HandleAsync(CloseTermCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var term = await terms.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (term is null)
        {
            return Result.Failure<TermDto>(Error.NotFound("term.not_found", "No term was found with that id."));
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
}
