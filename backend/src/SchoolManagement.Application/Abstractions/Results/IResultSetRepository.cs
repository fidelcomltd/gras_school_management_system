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
}
