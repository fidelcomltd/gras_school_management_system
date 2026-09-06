namespace SchoolManagement.Application.Idempotency;

/// <summary>How a claim attempt resolved.</summary>
public enum IdempotencyClaimOutcome
{
    /// <summary>No prior row existed for this key/caller; the caller should proceed and later call
    /// <see cref="IIdempotencyStore.CompleteAsync"/>.</summary>
    Claimed,

    /// <summary>A prior row exists, its fingerprint matches, and it is already completed — replay it.</summary>
    Replay,

    /// <summary>A prior row exists but its fingerprint does NOT match — same key, different request.</summary>
    Conflict,

    /// <summary>A prior row exists, its fingerprint matches, but it is not yet completed — another
    /// request with this exact key is still in flight.</summary>
    InProgress,
}

/// <summary>The response captured against a completed idempotency claim, for storage or replay.</summary>
/// <param name="StatusCode">The HTTP status code the protected handler produced.</param>
/// <param name="ContentType">The response content type, if any.</param>
/// <param name="BodyJson">
/// The response body as JSON, already REDACTED of anything marked
/// <see cref="RedactFromIdempotencyReplayAttribute"/> — see
/// <c>SchoolManagement.Api.Idempotency</c> for where that redaction happens. <see langword="null"/>
/// when the response carried no body.
/// </param>
/// <param name="Location">The response's <c>Location</c> header value, if it set one.</param>
public sealed record IdempotencyStoredResponse(int StatusCode, string? ContentType, string? BodyJson, string? Location);

/// <summary>The outcome of attempting to claim an idempotency key, per <see cref="IIdempotencyStore.TryClaimAsync"/>.</summary>
/// <param name="Outcome">Which of the four cases this is.</param>
/// <param name="Replay">The stored response to replay verbatim. Only set when <paramref name="Outcome"/> is <see cref="IdempotencyClaimOutcome.Replay"/>.</param>
public sealed record IdempotencyClaim(IdempotencyClaimOutcome Outcome, IdempotencyStoredResponse? Replay)
{
    /// <summary>No prior claim existed; proceed with the request.</summary>
    public static readonly IdempotencyClaim Claimed = new(IdempotencyClaimOutcome.Claimed, null);

    /// <summary>Same key, a different request — reject, never replay.</summary>
    public static readonly IdempotencyClaim Conflict = new(IdempotencyClaimOutcome.Conflict, null);

    /// <summary>Same key, same request, still being handled elsewhere — reject with a retriable 409.</summary>
    public static readonly IdempotencyClaim InProgress = new(IdempotencyClaimOutcome.InProgress, null);

    /// <summary>Same key, same request, already handled — replay <paramref name="response"/>.</summary>
    public static IdempotencyClaim ReplayWith(IdempotencyStoredResponse response) =>
        new(IdempotencyClaimOutcome.Replay, response);
}

/// <summary>
/// The idempotency substrate's persistence port (TASK-0019, approved delta
/// <c>decisions/2026-Q3-contract-deltas.md</c> "TASK-0019 / TASK-0027"). One implementation, called
/// once in the API layer by the endpoint filter behind <c>RequireIdempotencyKeyExtensions</c> —
/// never per-endpoint.
/// </summary>
/// <remarks>
/// Every member is deliberately isolated from the ambient mediator transaction: a claim must commit
/// (or fail on a real database constraint) BEFORE the protected handler runs, and completion must
/// commit AFTER it returns, regardless of whether the handler's own unit-of-work transaction
/// committed or rolled back moments earlier on the same logical request. See
/// <c>SchoolManagement.Infrastructure.Persistence.Repositories.IdempotencyStore</c>'s remarks for how
/// (the same isolated-<c>DbContext</c> technique <c>AdminAccountRepository.PersistLockoutStateAsync</c>
/// already uses, for the same reason).
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>
    /// Attempts to claim <paramref name="key"/> for <paramref name="caller"/>. Real, database-level
    /// concurrency safe: two genuinely simultaneous callers with the same key/caller resolve to
    /// exactly one <see cref="IdempotencyClaimOutcome.Claimed"/> and one
    /// <see cref="IdempotencyClaimOutcome.InProgress"/> (or a classification against whichever row
    /// won), never two claims.
    /// </summary>
    /// <param name="key">The raw <c>Idempotency-Key</c> header value.</param>
    /// <param name="caller">The caller's identity (an account id, or an anonymous sentinel).</param>
    /// <param name="fingerprint">The computed request fingerprint (not yet hashed).</param>
    /// <param name="now">The current instant.</param>
    /// <param name="retention">How long a claimed row should live before it is purge-eligible.</param>
    /// <param name="cancellationToken">Propagated to the underlying write.</param>
    Task<IdempotencyClaim> TryClaimAsync(
        string key,
        string caller,
        string fingerprint,
        DateTimeOffset now,
        TimeSpan retention,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the protected handler's response against a previously <see cref="IdempotencyClaimOutcome.Claimed"/>
    /// row, making it replayable. A no-op if the row cannot be found (see the implementation's
    /// remarks for why that is treated as safe rather than an error).
    /// </summary>
    Task CompleteAsync(
        string key,
        string caller,
        IdempotencyStoredResponse response,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Hard-deletes every row whose <c>ExpiresAtUtc</c> is at or before <paramref name="now"/> (§9.9:
    /// "an indefinite table is a defect, not a deferral"). Returns the number of rows removed, so the
    /// caller (<see cref="IdempotencyPurgeJob"/>) can record it on the purge's audit event.
    /// </summary>
    Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
