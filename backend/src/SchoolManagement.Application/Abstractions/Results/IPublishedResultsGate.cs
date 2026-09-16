namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>
/// How many <c>result_set</c> rows are <c>Published</c> in a given academic session — spec 6.2.9:
/// "the system checks whether any result set in the active session is Published... On save, a
/// confirmation dialogue names the concrete consequence and requires a reason of at least ten
/// characters."
/// </summary>
/// <remarks>
/// Same seam pattern and the same reasoning as <c>ISubjectScoreSessionLockLookup</c> — no
/// <c>result_set</c>/publish module exists in this codebase yet, so zero published result sets is
/// honestly today's answer everywhere, not a stand-in. <c>GET /settings/impact</c> (spec 6.2.12,
/// "the interface calls this to build the warnings") is NOT built by TASK-0069 — its own card's
/// approved contract delta named only <c>/settings/grading</c> and <c>/settings/assessment</c> — this
/// port exists solely so <c>PUT /settings/grading</c>, <c>POST /settings/grading/reset</c> and
/// <c>PUT /settings/assessment</c> can enforce the SAVE-TIME half of 6.2.9 (the conditional reason
/// requirement) for real, ahead of the banner/impact-summary UI that a later card builds.
/// </remarks>
public interface IPublishedResultsGate
{
    /// <summary>
    /// Returns how many result sets are <c>Published</c> in the session identified by
    /// <paramref name="sessionId"/>.
    /// </summary>
    /// <param name="sessionId">The academic session to check.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<int> CountPublishedInSessionAsync(Guid sessionId, CancellationToken cancellationToken);
}
