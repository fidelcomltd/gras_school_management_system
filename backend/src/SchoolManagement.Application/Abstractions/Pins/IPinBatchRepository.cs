using SchoolManagement.Domain.Pins;

namespace SchoolManagement.Application.Abstractions.Pins;

/// <summary>Per-batch pin counts for the list view (spec 6.8.11).</summary>
public sealed record PinBatchCounts(int Used, int Exhausted, int Suspended, int Revoked);

/// <summary>Persistence port for <see cref="PinBatch"/> and its <see cref="Pin"/>s.</summary>
public interface IPinBatchRepository
{
    /// <summary>Stages a new batch and its pins. Does NOT commit.</summary>
    Task AddAsync(PinBatch batch, IReadOnlyList<Pin> pins, CancellationToken cancellationToken);

    /// <summary>A tracked batch, or null.</summary>
    Task<PinBatch?> FindTrackedAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>A read-only batch, or null.</summary>
    Task<PinBatch?> FindReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Whether a batch in <paramref name="sessionId"/> already has this name, compared case-insensitively.</summary>
    Task<bool> NameExistsInSessionAsync(Guid sessionId, string name, CancellationToken cancellationToken);

    /// <summary>How many batches the session has, for the default name's sequence.</summary>
    Task<int> CountInSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Which of <paramref name="lookupKeys"/> already exist on ANY pin, revoked included, since the unique index covers them all (spec 6.8.6).</summary>
    Task<IReadOnlySet<string>> FindExistingLookupKeysAsync(IReadOnlyCollection<string> lookupKeys, CancellationToken cancellationToken);

    /// <summary>Newest first, keyset on id (UUIDv7 sorts by time).</summary>
    Task<IReadOnlyList<PinBatch>> ListAsync(Guid? sessionId, PinBatchState? state, Guid? beforeId, int pageSize, CancellationToken cancellationToken);

    /// <summary>Counts for each of <paramref name="batchIds"/>, in one grouped query.</summary>
    Task<IReadOnlyDictionary<Guid, PinBatchCounts>> CountPinsAsync(IReadOnlyCollection<Guid> batchIds, CancellationToken cancellationToken);

    /// <summary>A batch's pins, oldest first. Tracked when the caller will change them.</summary>
    Task<IReadOnlyList<Pin>> ListPinsAsync(Guid batchId, bool tracked, CancellationToken cancellationToken);

    /// <summary>A tracked pin, or null.</summary>
    Task<Pin?> FindPinTrackedAsync(Guid pinId, CancellationToken cancellationToken);
}
