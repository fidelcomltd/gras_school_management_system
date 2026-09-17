namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>
/// Whether any <c>subject_score</c> has been entered anywhere in a given academic session — spec
/// 6.2.6's session lock: "once the first mark is entered anywhere in a session, the set of components
/// and their maximums are locked for the whole of that session."
/// </summary>
/// <remarks>
/// TASK-0076 dispatch A replaced the Infrastructure implementation with a real query against
/// <c>subject_score</c>/<c>result_set</c>, now that both tables exist — see
/// <c>SchoolManagement.Infrastructure.Results.SubjectScoreSessionLockLookup</c>. Before this card the
/// stand-in honestly answered <see langword="false"/> unconditionally: no <c>subject_score</c> table
/// existed anywhere in this codebase, so that was today's only correct answer, not a placeholder one
/// (the same reasoning the 2026-09-15 drift entry applied to published-result snapshots).
/// </remarks>
public interface ISubjectScoreSessionLockLookup
{
    /// <summary>
    /// Returns <see langword="true"/> when at least one score has been entered anywhere in the session
    /// identified by <paramref name="sessionId"/>.
    /// </summary>
    /// <param name="sessionId">The academic session to check.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<bool> AnyScoreExistsInSessionAsync(Guid sessionId, CancellationToken cancellationToken);
}
