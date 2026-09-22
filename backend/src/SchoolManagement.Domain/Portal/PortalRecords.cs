using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Portal;

/// <summary>
/// One viewing session (spec 6.8.5 <c>pin_use</c>, 6.9.5): a pin opened one pupil for thirty minutes, counting one use.
/// The browser holds a random token; only its SHA-256 is stored, and the pin value is never stored here.
/// </summary>
public sealed class PinUse : Entity<Guid>
{
    /// <summary>Spec 6.9.5: a fixed window, not extended by activity.</summary>
    public static readonly TimeSpan SessionLength = TimeSpan.FromMinutes(30);

    private PinUse(Guid id, Guid pinId, Guid pupilId, string tokenHash, DateTimeOffset openedAtUtc, string? sourceAddress, string? userAgent)
        : base(id)
    {
        PinId = pinId;
        PupilId = pupilId;
        TokenHash = tokenHash;
        OpenedAtUtc = openedAtUtc;
        ExpiresAtUtc = openedAtUtc + SessionLength;
        SourceAddress = sourceAddress;
        UserAgent = userAgent;
        ViewedJson = "[]";
    }

    // EF Core materialisation constructor.
    private PinUse()
        : base()
    {
        TokenHash = string.Empty;
        ViewedJson = "[]";
    }

    /// <summary>The pin that opened the session.</summary>
    public Guid PinId { get; private set; }

    /// <summary>The one pupil it is bound to.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>Hex SHA-256 of the cookie token. Unique.</summary>
    public string TokenHash { get; private set; }

    /// <summary>When the use was counted.</summary>
    public DateTimeOffset OpenedAtUtc { get; private set; }

    /// <summary>Thirty minutes after opening.</summary>
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    /// <summary>Set by an early end (spec 6.9.9 <c>POST /portal/end</c>).</summary>
    public DateTimeOffset? EndedAtUtc { get; private set; }

    /// <summary>Truncated: an IPv4 /24 or IPv6 /48.</summary>
    public string? SourceAddress { get; private set; }

    /// <summary>Truncated to 120 characters.</summary>
    public string? UserAgent { get; private set; }

    /// <summary>JSON array of what was viewed in the session (terms, PDFs), for the usage report.</summary>
    public string ViewedJson { get; private set; }

    /// <summary>Opens a session. The caller has counted the use on the pin in the same transaction.</summary>
    public static PinUse Open(Guid pinId, Guid pupilId, string tokenHash, DateTimeOffset now, string? sourceAddress, string? userAgent) =>
        new(Guid.CreateVersion7(), pinId, pupilId, tokenHash, now, sourceAddress, Truncate(userAgent, 120));

    /// <summary>Whether it can still be used at <paramref name="now"/>.</summary>
    public bool IsOpenAt(DateTimeOffset now) => EndedAtUtc is null && now < ExpiresAtUtc;

    /// <summary>Ends the session early.</summary>
    public void End(DateTimeOffset now) => EndedAtUtc ??= now;

    /// <summary>Replaces the viewed list (a small JSON array the caller maintains).</summary>
    public void RecordViewed(string viewedJson) => ViewedJson = viewedJson;

    private static string? Truncate(string? value, int length) =>
        value is null ? null : value.Length <= length ? value : value[..length];
}

/// <summary>Spec 6.9.3's attempt outcomes, including the ones that are not failures.</summary>
public enum PortalAttemptOutcome
{
    /// <summary>A session opened and a use was counted.</summary>
    Success,

    /// <summary>Unknown number or unrecognised pin: the one outcome counted towards the rate limits.</summary>
    NotFound,

    /// <summary>Pin has no uses left.</summary>
    PinExhausted,

    /// <summary>Pin suspended by the spread control.</summary>
    PinSuspended,

    /// <summary>Pin revoked.</summary>
    PinRevoked,

    /// <summary>Pin's session has closed.</summary>
    PinExpired,

    /// <summary>Validated, but nothing published for the pupil; no use counted.</summary>
    NotPublished,

    /// <summary>Refused by the per-address block.</summary>
    BlockedAddress,

    /// <summary>Refused by the per-registration-number block.</summary>
    BlockedRegistrationNumber,
}

/// <summary>
/// One portal lookup attempt, successful or not (spec 6.9.3). It feeds the rate limits and the bursar's failure history,
/// and is purged after 90 days.
/// </summary>
public sealed class PortalAttempt : Entity<Guid>
{
    /// <summary>Spec 6.9.3.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    private PortalAttempt(Guid id, string registrationNumber, string? pinPrefix, Guid? pinId, PortalAttemptOutcome outcome, string? sourceAddress, DateTimeOffset attemptedAtUtc)
        : base(id)
    {
        RegistrationNumber = registrationNumber;
        PinPrefix = pinPrefix;
        PinId = pinId;
        Outcome = outcome;
        SourceAddress = sourceAddress;
        AttemptedAtUtc = attemptedAtUtc;
    }

    // EF Core materialisation constructor.
    private PortalAttempt()
        : base()
    {
        RegistrationNumber = string.Empty;
    }

    /// <summary>What was typed, normalised, capped at 40 characters.</summary>
    public string RegistrationNumber { get; private set; }

    /// <summary>The first four characters of what was typed as the pin.</summary>
    public string? PinPrefix { get; private set; }

    /// <summary>The pin, when the value matched one.</summary>
    public Guid? PinId { get; private set; }

    /// <summary>What happened.</summary>
    public PortalAttemptOutcome Outcome { get; private set; }

    /// <summary>Truncated source address.</summary>
    public string? SourceAddress { get; private set; }

    /// <summary>When.</summary>
    public DateTimeOffset AttemptedAtUtc { get; private set; }

    /// <summary>Records an attempt.</summary>
    public static PortalAttempt Record(string registrationNumber, string? pinPrefix, Guid? pinId, PortalAttemptOutcome outcome, string? sourceAddress, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), registrationNumber.Length <= 40 ? registrationNumber : registrationNumber[..40], pinPrefix, pinId, outcome, sourceAddress, now);
}
