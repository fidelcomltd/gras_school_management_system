using SchoolManagement.Domain.Classes;

namespace SchoolManagement.Application.Abstractions.Classes;

/// <summary>Persistence port for <see cref="Arm"/>.</summary>
/// <remarks>
/// Every read loads the FULL set and filters/sorts/pages in the Application handler, the same
/// deliberate choice <c>IClassLevelRepository</c> made — an arm register is admin-configuration-sized
/// (a handful of levels times a handful of arms times a handful of sessions), not a growing
/// pupil-scale table.
/// </remarks>
public interface IArmRepository
{
    /// <summary>Adds a new arm. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(Arm arm, CancellationToken cancellationToken);

    /// <summary>Loads a TRACKED arm by id, for a command that will mutate it.</summary>
    Task<Arm?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a read-only projection-friendly arm by id, for a query. <c>AsNoTracking</c>.</summary>
    Task<Arm?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Removes <paramref name="arm"/> permanently. No <c>SaveChangesAsync</c>.</summary>
    Task RemoveAsync(Arm arm, CancellationToken cancellationToken);

    /// <summary>Every arm, <c>AsNoTracking</c> — for the list/get/next-label queries.</summary>
    Task<IReadOnlyList<Arm>> ListAllReadOnlyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Every TRACKED arm belonging to <paramref name="sessionId"/> — used to close every arm in the
    /// same transaction that closes the session (spec 6.4.7).
    /// </summary>
    Task<IReadOnlyList<Arm>> ListBySessionTrackedAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="normalizedLabelKey"/> (already lower-invariant) is already used by
    /// another arm under the same <paramref name="classLevelId"/> and <paramref name="sessionId"/>
    /// (spec 6.4.3, 6.4.8: unique within level and session, case-insensitive).
    /// </summary>
    Task<bool> LabelExistsAsync(
        Guid classLevelId,
        Guid sessionId,
        string normalizedLabelKey,
        Guid? excludingId,
        CancellationToken cancellationToken);

    /// <summary>Whether any arm (any status) currently exists for <paramref name="sessionId"/> (spec 6.3.6's open-term precondition).</summary>
    Task<bool> AnyForSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Whether any arm (any status, any session) currently exists under <paramref name="classLevelId"/> (delete-level's reference check).</summary>
    Task<bool> AnyForLevelAsync(Guid classLevelId, CancellationToken cancellationToken);

    /// <summary>The number of arms (any status) that exist for <paramref name="sessionId"/> — spec 6.3.8's session arm count.</summary>
    Task<int> CountBySessionAsync(Guid sessionId, CancellationToken cancellationToken);
}
