using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Auth;

/// <summary>
/// A server-side session record (spec 6.1.11, spec 9.1): "Session tokens are 32 bytes from a
/// cryptographically secure source, stored hashed server-side, and rotated on privilege change and on
/// password change." This entity stores only the HASH of the token; the raw value lives solely in the
/// <c>__Host-Session</c> cookie and is never persisted or logged.
/// </summary>
/// <remarks>
/// <para>
/// A revoked or expired row is kept, never deleted — that is what lets
/// <c>IAdminSessionAuthenticator</c> distinguish the three 401
/// variants the approved contract delta requires (§3): a token that never matches any row at all is
/// <c>authentication.required</c>; a token whose row exists but is revoked is
/// <c>authentication.session_revoked</c>; one whose row is merely past its deadline is
/// <c>authentication.session_expired</c>.
/// </para>
/// <para>
/// Not an <see cref="IAuditableEntity"/>: a session row is written and revoked directly by the
/// handlers that own those transitions, and the audit trail that matters here is
/// <see cref="RevokedReason"/>/<see cref="RevokedAtUtc"/>, not a generic created/modified-by pair.
/// </para>
/// </remarks>
public sealed class AdminSession : Entity<Guid>
{
    private AdminSession(
        Guid id,
        Guid adminAccountId,
        string tokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset idleExpiresAtUtc,
        DateTimeOffset absoluteExpiresAtUtc)
        : base(id)
    {
        AdminAccountId = adminAccountId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        IdleExpiresAtUtc = idleExpiresAtUtc;
        AbsoluteExpiresAtUtc = absoluteExpiresAtUtc;
    }

    // EF Core materialisation constructor.
    private AdminSession()
        : base() => TokenHash = null!;

    /// <summary>The account this session authenticates.</summary>
    public Guid AdminAccountId { get; private set; }

    /// <summary>SHA-256 hex digest of the raw opaque session token. Never the raw value.</summary>
    public string TokenHash { get; private set; }

    /// <summary>When the session was created (sign-in, or password-change rotation keeps this unchanged).</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// Idle deadline. Extended only by a proactive <c>POST /auth/refresh</c> (approved delta §3a),
    /// never past <see cref="AbsoluteExpiresAtUtc"/>.
    /// </summary>
    public DateTimeOffset IdleExpiresAtUtc { get; private set; }

    /// <summary>Absolute deadline, fixed at creation (spec 6.1.11's 8-hour cap). Never extended.</summary>
    public DateTimeOffset AbsoluteExpiresAtUtc { get; private set; }

    /// <summary><c>null</c> while live; set once the session is revoked.</summary>
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    /// <summary>Why the session was revoked — a constant from <see cref="AdminSessionRevocationReasons"/>.</summary>
    public string? RevokedReason { get; private set; }

    /// <summary>Whether this session has been explicitly revoked.</summary>
    public bool IsRevoked => RevokedAtUtc is not null;

    /// <summary>Whether the idle or absolute deadline has passed at <paramref name="now"/>.</summary>
    public bool IsExpiredAt(DateTimeOffset now) => now >= IdleExpiresAtUtc || now >= AbsoluteExpiresAtUtc;

    /// <summary>Creates a new session, fixing its absolute deadline and starting idle deadline.</summary>
    public static AdminSession Create(Guid id, Guid adminAccountId, string tokenHash, DateTimeOffset now) =>
        new(
            id,
            adminAccountId,
            tokenHash,
            now,
            now + AuthPolicy.IdleTimeout,
            now + AuthPolicy.AbsoluteTimeout);

    /// <summary>
    /// Extends the idle deadline from <paramref name="now"/>, capped at the (unchanged) absolute
    /// deadline — a proactive <c>POST /auth/refresh</c> can never outrun the 8-hour cap.
    /// </summary>
    public void ExtendIdle(DateTimeOffset now)
    {
        var candidate = now + AuthPolicy.IdleTimeout;
        IdleExpiresAtUtc = candidate > AbsoluteExpiresAtUtc ? AbsoluteExpiresAtUtc : candidate;
    }

    /// <summary>
    /// Replaces the stored token hash without changing the session's identity or absolute deadline —
    /// spec 9.1's "rotated on... password change", which rotates the token but is not a new session.
    /// </summary>
    public void RotateToken(string newTokenHash) => TokenHash = newTokenHash;

    /// <summary>Marks the session revoked. Idempotent: a second call is a no-op.</summary>
    public void Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = now;
        RevokedReason = reason;
    }
}
