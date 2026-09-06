using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolManagement.Application.Idempotency;
using SchoolManagement.Domain.Idempotency;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IIdempotencyStore"/>.</summary>
/// <remarks>
/// <para>
/// EVERY OPERATION OPENS ITS OWN <see cref="ApplicationDbContext"/> over the shared
/// <see cref="DbContextOptions{TContext}"/>, exactly the technique
/// <c>AdminAccountRepository.PersistLockoutStateAsync</c> already uses and for the identical reason:
/// a claim must commit — or fail on the real unique constraint — independently of whatever
/// transaction the ambient mediator pipeline's <c>UnitOfWorkBehavior</c> has open on the request's
/// scoped context. Sharing that context would mean the claim's own "insert" is not durable (and
/// therefore not visible to a genuinely concurrent second request) until the WHOLE command commits,
/// which defeats the point of claiming BEFORE the protected handler runs.
/// </para>
/// <para>
/// CONCURRENCY: <see cref="TryClaimAsync"/> does a read-then-write, which is racy on its own — two
/// simultaneous callers can both see "no row" and both attempt to insert. The UNIQUE index on
/// <c>(key_hash, caller)</c> (<see cref="Configurations.IdempotencyRecordConfiguration"/>) is what
/// actually holds the line: the loser's insert throws a real PostgreSQL unique-violation, caught
/// below, and re-classified against whichever row won. This is the same pattern
/// <c>PersistenceErrors</c> documents as the safety net behind a handler's own conflict check — here
/// it IS the mechanism, not a backstop, because there is no handler-level check to fall back on.
/// </para>
/// </remarks>
internal sealed class IdempotencyStore(DbContextOptions<ApplicationDbContext> options) : IIdempotencyStore
{
    private const string UniqueViolationSqlState = "23505";

    /// <inheritdoc />
    public async Task<IdempotencyClaim> TryClaimAsync(
        string key,
        string caller,
        string fingerprint,
        DateTimeOffset now,
        TimeSpan retention,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(caller);
        ArgumentException.ThrowIfNullOrEmpty(fingerprint);

        var keyHash = Hash(key);
        var fingerprintHash = Hash(fingerprint);

        await using var context = new ApplicationDbContext(options);

        var existing = await FindAsync(context, keyHash, caller, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            return Classify(existing, fingerprintHash);
        }

        var record = IdempotencyRecord.Reserve(
            Guid.CreateVersion7(),
            keyHash,
            caller,
            fingerprintHash,
            now,
            retention);

        context.IdempotencyRecords.Add(record);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Lost a genuine race: another request's claim committed between our read and our
            // write. Re-read (a FRESH context — `record`'s failed insert left this one's tracking
            // state unusable) and classify against the winner, exactly as the "existing is not
            // null" branch above would have if our read had happened a moment later.
            await using var raceContext = new ApplicationDbContext(options);

            var winner = await FindAsync(raceContext, keyHash, caller, cancellationToken)
                .ConfigureAwait(false);

            // The violation proves a row now exists; null here would mean it was purged in the
            // same instant, vanishingly rare for a row that is at most 24h from being fresh Never
            // treated as a hard failure — proceeding as a fresh claim is safe, just slower.
            return winner is null ? IdempotencyClaim.Claimed : Classify(winner, fingerprintHash);
        }

        return IdempotencyClaim.Claimed;
    }

    /// <inheritdoc />
    public async Task CompleteAsync(
        string key,
        string caller,
        IdempotencyStoredResponse response,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(caller);
        ArgumentNullException.ThrowIfNull(response);

        var keyHash = Hash(key);

        await using var context = new ApplicationDbContext(options);

        // Tracked (no AsNoTracking): Complete() mutates it and SaveChangesAsync below persists that.
        var record = await context.IdempotencyRecords
            .Where(row => row.KeyHash == keyHash && row.Caller == caller)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            // The reservation this completes is created by TryClaimAsync moments earlier in the
            // same request; a missing row here would mean it was purged mid-request, which the
            // retention window (hours) makes vanishingly unlikely. Doing nothing is safer than
            // throwing after the protected handler has already produced the caller's real response.
            return;
        }

        record.Complete(response.StatusCode, response.ContentType, response.BodyJson, response.Location, now);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var context = new ApplicationDbContext(options);

        var expired = await context.IdempotencyRecords
            .Where(record => record.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (expired.Count == 0)
        {
            return 0;
        }

        context.IdempotencyRecords.RemoveRange(expired);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return expired.Count;
    }

    private static Task<IdempotencyRecord?> FindAsync(
        ApplicationDbContext context,
        string keyHash,
        string caller,
        CancellationToken cancellationToken) =>
        context.IdempotencyRecords
            .AsNoTracking()
            .Where(record => record.KeyHash == keyHash && record.Caller == caller)
            .FirstOrDefaultAsync(cancellationToken);

    private static IdempotencyClaim Classify(IdempotencyRecord existing, string fingerprintHash)
    {
        if (!string.Equals(existing.FingerprintHash, fingerprintHash, StringComparison.Ordinal))
        {
            return IdempotencyClaim.Conflict;
        }

        if (!existing.IsCompleted)
        {
            return IdempotencyClaim.InProgress;
        }

        return IdempotencyClaim.ReplayWith(new IdempotencyStoredResponse(
            existing.ResponseStatusCode!.Value,
            existing.ResponseContentType,
            existing.ResponseBodyJson,
            existing.ResponseLocation));
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: UniqueViolationSqlState };

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }
}
