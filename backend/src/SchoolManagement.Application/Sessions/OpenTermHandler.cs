using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Sessions;

/// <summary>
/// Handles <see cref="OpenTermCommand"/>.
/// </summary>
/// <remarks>
/// DEFERRED (spec 6.3.6, no task card yet): "at least one arm exists for the session" is one of
/// open's three preconditions. <c>Arm</c> is spec 06 §6.4 and does not exist anywhere in this
/// codebase, so it is not checked below — this makes <c>open</c> MORE PERMISSIVE than spec until the
/// arms card lands. Deliberate and visible, not a silent omission: see TASK-0035's Log and STATE.md
/// <c>## Known drift</c> for the tracked entry. The third precondition ("the session has a start and
/// end date") needs no runtime check at all — <see cref="AcademicSession.Create"/> makes both dates
/// mandatory, so it is structurally guaranteed rather than merely tested.
/// </remarks>
internal sealed class OpenTermHandler(
    ITermRepository terms,
    IAcademicSessionRepository sessions,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<OpenTermCommand, Result<TermDto>>
{
    /// <inheritdoc />
    public async Task<Result<TermDto>> HandleAsync(OpenTermCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var term = await terms.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (term is null)
        {
            return Result.Failure<TermDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var session = await sessions.FindTrackedByIdAsync(term.SessionId, cancellationToken).ConfigureAwait(false);

        if (session is null)
        {
            return Result.Failure<TermDto>(Error.Failure("term.orphaned", "This term's session could not be found."));
        }

        Term? previousTermInSession = null;
        (string TermName, string SessionName)? activeElsewhere = null;

        // Captured BEFORE any mutation below, so it reflects the state the DB actually holds right
        // now — not a side effect of this same operation activating `session` in memory.
        AcademicSession? previouslyActiveSession = null;

        if (term.Ordinal > 1)
        {
            previousTermInSession = await terms
                .FindByOrdinalAsync(session.Id, term.Ordinal - 1, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            var activeTerm = await terms.FindActiveAsync(cancellationToken).ConfigureAwait(false);

            if (activeTerm is not null)
            {
                var activeSession = await sessions
                    .FindReadOnlyByIdAsync(activeTerm.SessionId, cancellationToken)
                    .ConfigureAwait(false);

                activeElsewhere = (activeTerm.Name, activeSession?.Name ?? "its session");
            }

            previouslyActiveSession = await sessions.FindActiveAsync(cancellationToken).ConfigureAwait(false);
        }

        var canOpen = TermTransitionGuard.CanOpen(term, session, previousTermInSession, activeElsewhere);

        if (canOpen.IsFailure)
        {
            return Result.Failure<TermDto>(canOpen.Error);
        }

        var open = term.Open();

        if (open.IsFailure)
        {
            return Result.Failure<TermDto>(open.Error);
        }

        if (term.Ordinal == 1)
        {
            session.Activate();

            // Spec 6.3.5: opening First Term "moves the new session to active and the old session to
            // closed" — the old session's own `state` flag, independent of whether it still has any
            // term flagged active (the "lame duck" window described on AcademicSession's remarks).
            if (previouslyActiveSession is not null && previouslyActiveSession.Id != session.Id)
            {
                previouslyActiveSession.Close();
            }
        }

        await auditSink.RecordAsync(
            Privileges.Term.Open,
            "term",
            term.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SessionMapper.ToTermDto(term));
    }
}
