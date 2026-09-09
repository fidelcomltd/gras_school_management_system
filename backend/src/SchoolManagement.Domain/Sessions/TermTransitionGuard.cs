using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Sessions;

/// <summary>
/// Pure decision logic for the cross-entity halves of open/reopen (spec 6.3.6) that need to see
/// beyond the term itself. The repository lookups that gather the facts below are the handler's
/// job; this only decides, given already-resolved facts, whether the transition is allowed and
/// names the reason (spec 6.3.6: "The block message names the reason").
/// </summary>
public static class TermTransitionGuard
{
    /// <summary>
    /// Spec 6.3.6: "Opening a term is blocked unless: the previous term in the same session is
    /// closed, or this is the first term of the session and the previous session is closed or has no
    /// active term; the session has a start and end date; and at least one arm exists for the
    /// session. The block message names the reason, for example: First Term 2026/2027 cannot be
    /// opened because Third Term 2025/2026 is still active. Close it first."
    /// </summary>
    /// <param name="term">The term being opened. Must already be <see cref="TermState.Upcoming"/>.</param>
    /// <param name="session"><paramref name="term"/>'s own session — named in the message.</param>
    /// <param name="previousTermInSession">
    /// The term at <c>Ordinal - 1</c> in the same session, or <see langword="null"/> when
    /// <paramref name="term"/> is ordinal 1 (that case is decided by
    /// <paramref name="activeTermElsewhere"/> instead).
    /// </param>
    /// <param name="activeTermElsewhere">
    /// The one globally active term (spec 6.3.9: at most one exists) and its session's name, if any.
    /// Only consulted when <paramref name="term"/> is ordinal 1 — "the previous session is closed or
    /// has no active term" reduces to "no term anywhere is currently active," since a session cannot
    /// hold an active term while itself being anything other than <see cref="SessionState.Active"/>.
    /// </param>
    /// <param name="hasArmsForSession">
    /// TASK-0039: whether at least one arm (any status) exists for <paramref name="session"/> — spec
    /// 6.3.6's third precondition. Was DEFERRED (always treated as satisfied) until <c>Arm</c> existed;
    /// see <c>OpenTermHandler</c>'s remarks for the resolved seam.
    /// </param>
    public static Result CanOpen(
        Term term,
        AcademicSession session,
        Term? previousTermInSession,
        (string TermName, string SessionName)? activeTermElsewhere,
        bool hasArmsForSession)
    {
        ArgumentNullException.ThrowIfNull(term);
        ArgumentNullException.ThrowIfNull(session);

        if (term.State != TermState.Upcoming)
        {
            return Result.Failure(Error.Conflict(
                "term.open_invalid_state",
                $"{term.Name} {session.Name} is {term.State} and cannot be opened."));
        }

        if (term.Ordinal == 1)
        {
            if (activeTermElsewhere is { } active)
            {
                return Result.Failure(Error.Conflict(
                    "term.previous_session_still_active",
                    $"{term.Name} {session.Name} cannot be opened because {active.TermName} " +
                    $"{active.SessionName} is still active. Close it first."));
            }
        }
        else if (previousTermInSession is null || previousTermInSession.State != TermState.Closed)
        {
            var previousDescription = previousTermInSession is null
                ? "the previous term"
                : $"{previousTermInSession.Name} {session.Name}";
            var stateDescription = previousTermInSession switch
            {
                null => "not yet opened",
                { State: TermState.Active } => "still active",
                _ => "not yet closed",
            };

            return Result.Failure(Error.Conflict(
                "term.previous_term_not_closed",
                $"{term.Name} {session.Name} cannot be opened because {previousDescription} is " +
                $"{stateDescription}. Close it first."));
        }

        // TASK-0039, spec 6.3.6's third precondition — checked LAST, after every other reason has
        // already had a chance to fire its own, more specific message.
        if (!hasArmsForSession)
        {
            return Result.Failure(Error.Conflict(
                "term.no_arms_for_session",
                $"No arms exist for {session.Name}. Create at least one arm before opening a term."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Spec 6.3.6: reopening "is refused outright if the following term has already been opened."
    /// The reason length and the <c>is_super_admin</c> gate are checked elsewhere (the command
    /// validator and the handler, respectively) — this only carries the state check and the
    /// following-term refusal, both pure given <paramref name="followingTermOpened"/>.
    /// </summary>
    /// <param name="term">The term being reopened. Must already be <see cref="TermState.Closed"/>.</param>
    /// <param name="followingTermOpened">
    /// Whether the term's successor has ever been opened. For ordinal 1 or 2 this is "the term at
    /// <c>Ordinal + 1</c> in the same session is not <see cref="TermState.Upcoming"/>." For ordinal 3
    /// there is no direct FK to "the next session" — the handler derives it from the session's own
    /// <see cref="SessionState"/>, which flips to <see cref="SessionState.Closed"/> exactly when a
    /// successor session's First Term opens (spec 6.3.5); see the handler's remarks and
    /// <c>backend/docs/ASSUMPTIONS.md</c> for that reading.
    /// </param>
    public static Result CanReopen(Term term, bool followingTermOpened)
    {
        ArgumentNullException.ThrowIfNull(term);

        if (term.State != TermState.Closed)
        {
            return Result.Failure(Error.Conflict(
                "term.reopen_invalid_state",
                $"Only a closed term can be reopened. {term.Name} is {term.State}."));
        }

        if (followingTermOpened)
        {
            return Result.Failure(Error.Conflict(
                "term.reopen_blocked_by_following_term",
                $"{term.Name} cannot be reopened because the following term has already been " +
                "opened."));
        }

        return Result.Success();
    }
}
