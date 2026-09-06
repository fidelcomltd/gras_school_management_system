using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Idempotency;

/// <summary>
/// One claimed <c>Idempotency-Key</c> (TASK-0019, root <c>CLAUDE.md</c> §6: "Mutating endpoints
/// that can be retried accept an <c>Idempotency-Key</c>."). A row is created the first time a key
/// is seen for a given caller, and completed once the protected handler has produced a response.
/// </summary>
/// <remarks>
/// <para>
/// STORES HASHES, NOT RAW VALUES. <see cref="KeyHash"/> and <see cref="FingerprintHash"/> are
/// SHA-256 digests of the client-supplied key and the computed request fingerprint, never the raw
/// strings — approved delta: "the stored row — key hash, fingerprint hash, caller, response body —
/// is never exposed by any endpoint." Hashing keeps an arbitrary client-supplied string out of the
/// database verbatim; equality (same key => same hash) is all the mechanism ever needs.
/// </para>
/// <para>
/// NOT an <see cref="IAuditableEntity"/> and not <see cref="ISoftDeletable"/>: this is ephemeral
/// infrastructure state, not a governed business entity, and the retention rule (§9.9) is a hard
/// DELETE on expiry (<c>IIdempotencyStore.PurgeExpiredAsync</c>), not a soft-delete flag.
/// </para>
/// <para>
/// <see cref="ResponseBodyJson"/> holds the REDACTED response — any property marked
/// <c>RedactFromIdempotencyReplayAttribute</c> is nulled out before storage, never the value the
/// live caller actually received. See <c>SchoolManagement.Api.Idempotency</c> for the redaction and
/// replay mechanics; this entity only stores whatever it is given.
/// </para>
/// </remarks>
public sealed class IdempotencyRecord : Entity<Guid>
{
    private IdempotencyRecord(
        Guid id,
        string keyHash,
        string caller,
        string fingerprintHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
        : base(id)
    {
        KeyHash = keyHash;
        Caller = caller;
        FingerprintHash = fingerprintHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    // EF Core materialisation constructor.
    private IdempotencyRecord()
        : base()
    {
        KeyHash = null!;
        Caller = null!;
        FingerprintHash = null!;
    }

    /// <summary>SHA-256 hex digest of the raw <c>Idempotency-Key</c> header value.</summary>
    public string KeyHash { get; private set; }

    /// <summary>
    /// The caller this key was claimed under (the authenticated account id, or an anonymous
    /// sentinel). Part of the claim's identity — approved delta: "the same string from two accounts
    /// is two requests" — so it is stored in the clear rather than hashed; it is an opaque id, not
    /// the kind of arbitrary client-supplied value the key and fingerprint are.
    /// </summary>
    public string Caller { get; private set; }

    /// <summary>
    /// SHA-256 hex digest of the request fingerprint (method + path + caller + normalised body).
    /// A second claim under the same <see cref="KeyHash"/>/<see cref="Caller"/> pair with a
    /// DIFFERENT fingerprint is <c>idempotency.key_conflict</c>, never a replay.
    /// </summary>
    public string FingerprintHash { get; private set; }

    /// <summary>When this key was first claimed.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// When this row becomes eligible for purge (§9.9). Fixed at claim time from the configured
    /// retention window; never extended by a replay.
    /// </summary>
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    /// <summary><see langword="null"/> while the protected handler is still running.</summary>
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    /// <summary>The completed handler's HTTP status code. <see langword="null"/> until completed.</summary>
    public int? ResponseStatusCode { get; private set; }

    /// <summary>The completed handler's response content type, if any.</summary>
    public string? ResponseContentType { get; private set; }

    /// <summary>The completed handler's REDACTED response body, serialised as JSON. May be null (no body).</summary>
    public string? ResponseBodyJson { get; private set; }

    /// <summary>The completed handler's <c>Location</c> header value, if it set one.</summary>
    public string? ResponseLocation { get; private set; }

    /// <summary>Whether the protected handler has finished and a response is available to replay.</summary>
    public bool IsCompleted => CompletedAtUtc is not null;

    /// <summary>Claims a new key. The row starts uncompleted — see <see cref="Complete"/>.</summary>
    public static IdempotencyRecord Reserve(
        Guid id,
        string keyHash,
        string caller,
        string fingerprintHash,
        DateTimeOffset now,
        TimeSpan retention) =>
        new(id, keyHash, caller, fingerprintHash, now, now + retention);

    /// <summary>Records the protected handler's (already-redacted) response, making this row replayable.</summary>
    public void Complete(
        int statusCode,
        string? contentType,
        string? redactedBodyJson,
        string? location,
        DateTimeOffset now)
    {
        ResponseStatusCode = statusCode;
        ResponseContentType = contentType;
        ResponseBodyJson = redactedBodyJson;
        ResponseLocation = location;
        CompletedAtUtc = now;
    }
}
