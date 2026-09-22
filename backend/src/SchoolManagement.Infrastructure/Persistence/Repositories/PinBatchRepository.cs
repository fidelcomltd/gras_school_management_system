using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Pins;
using SchoolManagement.Domain.Pins;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

internal sealed class PinBatchRepository(ApplicationDbContext context) : IPinBatchRepository
{
    public Task AddAsync(PinBatch batch, IReadOnlyList<Pin> pins, CancellationToken cancellationToken)
    {
        context.PinBatches.Add(batch);
        context.Pins.AddRange(pins);
        return Task.CompletedTask;
    }

    public Task<PinBatch?> FindTrackedAsync(Guid id, CancellationToken cancellationToken) =>
        context.PinBatches.FirstOrDefaultAsync(batch => batch.Id == id, cancellationToken);

    public Task<PinBatch?> FindReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        context.PinBatches.AsNoTracking().FirstOrDefaultAsync(batch => batch.Id == id, cancellationToken);

    public Task<bool> NameExistsInSessionAsync(Guid sessionId, string name, CancellationToken cancellationToken)
    {
        // Case-insensitive exact match: ILIKE with the LIKE metacharacters escaped, so a name containing % or _ is literal.
        var pattern = name.Trim().Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return context.PinBatches.AnyAsync(batch => batch.SessionId == sessionId && EF.Functions.ILike(batch.Name, pattern), cancellationToken);
    }

    public Task<int> CountInSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        context.PinBatches.CountAsync(batch => batch.SessionId == sessionId, cancellationToken);

    public async Task<IReadOnlySet<string>> FindExistingLookupKeysAsync(IReadOnlyCollection<string> lookupKeys, CancellationToken cancellationToken)
    {
        var keys = lookupKeys.ToArray();
        var existing = await context.Pins.AsNoTracking()
            .Where(pin => keys.Contains(pin.LookupKey))
            .Select(pin => pin.LookupKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return existing.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<PinBatch>> ListAsync(
        Guid? sessionId, PinBatchState? state, Guid? beforeId, int pageSize, CancellationToken cancellationToken)
    {
        var query = context.PinBatches.AsNoTracking();
        if (sessionId is { } session)
        {
            query = query.Where(batch => batch.SessionId == session);
        }

        if (state is { } wanted)
        {
            query = query.Where(batch => batch.State == wanted);
        }

        if (beforeId is { } cursor)
        {
            query = query.Where(batch => batch.Id.CompareTo(cursor) < 0);
        }

        return await query.OrderByDescending(batch => batch.Id).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, PinBatchCounts>> CountPinsAsync(IReadOnlyCollection<Guid> batchIds, CancellationToken cancellationToken)
    {
        var ids = batchIds.ToArray();
        var rows = await context.Pins.AsNoTracking()
            .Where(pin => ids.Contains(pin.BatchId))
            .GroupBy(pin => pin.BatchId)
            .Select(group => new
            {
                BatchId = group.Key,
                Used = group.Count(pin => pin.UseCount > 0),
                Exhausted = group.Count(pin => pin.State == PinState.Exhausted),
                Suspended = group.Count(pin => pin.State == PinState.Suspended),
                Revoked = group.Count(pin => pin.State == PinState.Revoked),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.ToDictionary(row => row.BatchId, row => new PinBatchCounts(row.Used, row.Exhausted, row.Suspended, row.Revoked));
    }

    public async Task<IReadOnlyList<Pin>> ListPinsAsync(Guid batchId, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? context.Pins : context.Pins.AsNoTracking();
        return await query.Where(pin => pin.BatchId == batchId).OrderBy(pin => pin.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<Pin?> FindPinTrackedAsync(Guid pinId, CancellationToken cancellationToken) =>
        context.Pins.FirstOrDefaultAsync(pin => pin.Id == pinId, cancellationToken);
}
