namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>
/// How many <c>result_set</c> rows are <c>Published</c> in a given academic session — spec 6.2.9:
/// "the system checks whether any result set in the active session is Published... On save, a
/// confirmation dialogue names the concrete consequence and requires a reason of at least ten
/// characters."
/// </summary>
/// <remarks>
/// TASK-0076 dispatch A replaced the Infrastructure implementation with a real query against
/// <c>result_set</c>, now that it exists — see
/// <c>SchoolManagement.Infrastructure.Results.PublishedResultsGate</c>. Before this card the stand-in
/// honestly answered zero unconditionally (same reasoning as <c>ISubjectScoreSessionLockLookup</c>).
/// <c>GET /settings/impact</c> (spec 6.2.12, "the interface calls this to build the warnings") is
/// STILL not built — this port exists solely so <c>PUT /settings/grading</c>,
/// <c>POST /settings/grading/reset</c> and <c>PUT /settings/assessment</c> can enforce the SAVE-TIME
/// half of 6.2.9 (the conditional reason requirement) for real, ahead of the banner/impact-summary UI
/// that a later card builds.
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

    /// <summary>
    /// Whether any <c>result_set</c> for the THIRD term (<c>Term.Ordinal == 3</c>) of the session
    /// identified by <paramref name="sessionId"/> is <c>Published</c>, for any arm — spec 6.2.8/6.2.10:
    /// "annual_method and its weights are locked once Third Term is published for any arm." TASK-0077.
    /// </summary>
    /// <param name="sessionId">The academic session to check.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<bool> AnyThirdTermPublishedInSessionAsync(Guid sessionId, CancellationToken cancellationToken);
}
