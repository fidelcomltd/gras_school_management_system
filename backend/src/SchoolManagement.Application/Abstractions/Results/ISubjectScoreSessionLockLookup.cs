namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>
/// Whether any <c>subject_score</c> has been entered anywhere in a given academic session — spec
/// 6.2.6's session lock: "once the first mark is entered anywhere in a session, the set of components
/// and their maximums are locked for the whole of that session."
/// </summary>
/// <remarks>
/// <para>
/// THIS IS A DOCUMENTED SEAM, NOT A STAND-IN PRETENDING TO BE REAL — a distinct pattern from
/// <c>IResultSetArmLookup</c>'s <c>NotYetImplementedResultSetArmLookup</c>, which THROWS because no
/// route reaches it yet (2026-08-26 drift entry). This port IS reached, on every
/// <c>PUT /settings/assessment</c> call, and throwing would break that endpoint outright. No
/// scoring/results module exists anywhere in this codebase as of TASK-0069 — no <c>subject_score</c>
/// table, no code path that could ever create one — so "no marks exist in any session" is not a
/// placeholder answer, it is today's only CORRECT answer, the same reasoning the 2026-09-15 drift
/// entry applies to published-result snapshots ("vacuously satisfied... nothing was invented to
/// satisfy the criterion").
/// </para>
/// <para>
/// Whichever future card first persists a <c>subject_score</c> row (not carded as of 2026-09-16) must
/// replace the Infrastructure implementation with a real query and add the test this seam cannot
/// carry today — see <c>backend/docs/ASSUMPTIONS.md</c>.
/// </para>
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
