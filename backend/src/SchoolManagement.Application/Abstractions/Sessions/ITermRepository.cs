using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Abstractions.Sessions;

/// <summary>Persistence port for <see cref="Term"/>.</summary>
public interface ITermRepository
{
    /// <summary>Adds a new term. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(Term term, CancellationToken cancellationToken);

    /// <summary>Loads a TRACKED term by id, for a command that will mutate it.</summary>
    Task<Term?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a read-only projection-friendly term by id, for a query. <c>AsNoTracking</c>.</summary>
    Task<Term?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the read-only sibling term at <paramref name="ordinal"/> within
    /// <paramref name="sessionId"/>, or <see langword="null"/> if none exists yet. Used for the
    /// previous/next-term chronology and open/reopen guards (spec 6.3.4, 6.3.6).
    /// </summary>
    Task<Term?> FindByOrdinalAsync(Guid sessionId, int ordinal, CancellationToken cancellationToken);

    /// <summary>All three terms of <paramref name="sessionId"/>, read-only, ordered by ordinal.</summary>
    Task<IReadOnlyList<Term>> ListBySessionReadOnlyAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// The one read-only term currently <see cref="TermState.Active"/> anywhere in the system, if any
    /// (spec 6.3.9: at most one — enforced by a partial unique index, not this lookup).
    /// </summary>
    Task<Term?> FindActiveAsync(CancellationToken cancellationToken);
}
