using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>
/// One arm's composed display name plus the state its result set is in, for spec 6.3.6's term-close
/// block message: "These arms have results that are not published: Primary 2B (Awaiting Approval),
/// Primary 5A (Draft)."
/// </summary>
/// <param name="ArmId">The blocking result set's arm.</param>
/// <param name="ArmDisplayName">The arm's composed display name, for the rejection message.</param>
/// <param name="State">The result set's current state.</param>
public sealed record BlockingResultSetSummary(Guid ArmId, string ArmDisplayName, ResultSetState State);

/// <summary>Persistence port for <see cref="ResultSet"/>.</summary>
public interface IResultSetRepository
{
    /// <summary>
    /// Every result set in <paramref name="termId"/> whose state is <see cref="ResultSetState.Draft"/>,
    /// <see cref="ResultSetState.AwaitingApproval"/> or <see cref="ResultSetState.Approved"/> — spec
    /// 6.3.6's term-close precondition: "Closing a term is blocked when any result set in the term is
    /// in state Draft, Awaiting Approval or Approved with marks entered." An arm with no result set
    /// row at all (no marks ever entered) never appears here, which is exactly how that precondition
    /// leaves an unscored nursery arm free to close.
    /// </summary>
    Task<IReadOnlyList<BlockingResultSetSummary>> ListBlockingTermCloseAsync(Guid termId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a TRACKED result set for <paramref name="armId"/>/<paramref name="termId"/> (TASK-0076
    /// dispatch B), for the score-sheet save handler, which may create the first row for this pair
    /// (spec 6.7.11 row 1) or mutate an existing one's <see cref="ResultSet.NeedsRecompute"/>.
    /// <see langword="null"/> when no result set exists yet for this arm and term ("Not started").
    /// </summary>
    Task<ResultSet?> FindTrackedByArmTermAsync(Guid armId, Guid termId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a TRACKED result set by its own id (TASK-0071) — <c>POST /result-sets/{resultSetId}/compute</c>
    /// is scoped by result set id directly, unlike the score-sheet routes which are scoped by arm.
    /// <see langword="null"/> when no result set exists with that id.
    /// </summary>
    Task<ResultSet?> FindTrackedByIdAsync(Guid resultSetId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a read-only result set for <paramref name="armId"/>/<paramref name="termId"/>, for the
    /// score-sheet read handler. <see langword="null"/> when none exists yet. <c>AsNoTracking</c>.
    /// </summary>
    Task<ResultSet?> FindReadOnlyByArmTermAsync(Guid armId, Guid termId, CancellationToken cancellationToken);

    /// <summary>Stages a brand-new result set for insertion. Does NOT commit.</summary>
    Task AddAsync(ResultSet resultSet, CancellationToken cancellationToken);

    /// <summary>
    /// Row-locks (<c>SELECT ... FOR UPDATE</c>) the result set for <paramref name="armId"/>/
    /// <paramref name="termId"/>, if one exists, and returns it TRACKED (TASK-0088 AC A4) — the same
    /// technique <c>AdminAccountRepository.LockActiveSuperAdminIdsAsync</c> uses. Every sheet save
    /// that checks <see cref="ResultSet.State"/> (score, void, trait, development, attendance,
    /// class-teacher remark) calls this instead of <see cref="FindTrackedByArmTermAsync"/>, so a
    /// concurrent settings/mapping flag or a concurrent compute cannot silently overwrite this save's
    /// eventual write — <c>result_set</c> carries no concurrency token of its own.
    /// <see langword="null"/> when no result set exists yet ("Not started"): there is nothing to lock,
    /// and the first save creates the row unlocked exactly as before.
    /// </summary>
    Task<ResultSet?> FindTrackedByArmTermForUpdateAsync(Guid armId, Guid termId, CancellationToken cancellationToken);

    /// <summary>
    /// Row-locks the result set by its own id (TASK-0088 AC A4) — used by
    /// <c>ComputeResultSetHandler</c>, which is scoped by result set id directly like
    /// <see cref="FindTrackedByIdAsync"/>. <see langword="null"/> when no result set exists with that id.
    /// </summary>
    Task<ResultSet?> FindTrackedByIdForUpdateAsync(Guid resultSetId, CancellationToken cancellationToken);

    /// <summary>
    /// Row-locks, in ASCENDING ID ORDER (TASK-0088 AC A4 — avoids deadlocking against another
    /// flagger or a concurrent compute locking the same rows in a different order), every result set
    /// in <paramref name="sessionId"/> whose state is not <see cref="ResultSetState.Published"/>, and
    /// returns them TRACKED, in that same id order. Used by the seven §6.2.9 settings handlers
    /// (AC A1): a settings save flags every non-Published set in the active session.
    /// </summary>
    Task<IReadOnlyList<ResultSet>> LockNonPublishedInSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Row-locks, in ASCENDING ID ORDER (see <see cref="LockNonPublishedInSessionAsync"/>'s remarks),
    /// every result set for <paramref name="termId"/> whose arm's class level is in
    /// <paramref name="classLevelIds"/> and whose state is not <see cref="ResultSetState.Published"/>,
    /// and returns them TRACKED in that order. Used by the subject-mapping handlers that can change
    /// more than one arm's subject list in one save (AC A2): <c>SaveSubjectMappingGridHandler</c> and
    /// <c>PrefillSubjectMappingsHandler</c> against their own term, <c>CopySubjectMappingsHandler</c>
    /// against its destination term. An empty <paramref name="classLevelIds"/> short-circuits to an
    /// empty result without a round trip — a save with no addition or ending touches no arm's subject
    /// list.
    /// </summary>
    Task<IReadOnlyList<ResultSet>> LockNonPublishedByTermAndClassLevelsAsync(
        Guid termId, IReadOnlyCollection<Guid> classLevelIds, CancellationToken cancellationToken);
}
