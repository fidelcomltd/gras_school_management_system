using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Pins;

/// <summary>Spec 6.8.5's pin states.</summary>
public enum PinState
{
    /// <summary>Never used.</summary>
    Unused,

    /// <summary>Used at least once, with uses left.</summary>
    Active,

    /// <summary>All uses spent.</summary>
    Exhausted,

    /// <summary>Set by the spread control (6.9.3). The only state an administrator can reverse.</summary>
    Suspended,

    /// <summary>Revoked with a reason, alone or with its batch. Terminal.</summary>
    Revoked,
}

/// <summary>
/// One access pin (spec 6.8.5). It has no pupil and no arm. The value itself is never stored in plaintext: the
/// Argon2id hash is the record of truth, <see cref="LookupKey"/> (a keyed HMAC) finds the row from the value alone,
/// and <see cref="Ciphertext"/> exists only until the batch's purge date, for reprinting.
/// </summary>
public sealed class Pin : Entity<Guid>
{
    /// <summary>Spec 6.8.5: the first four characters kept in plaintext for telephone identification.</summary>
    public const int PrefixLength = 4;

    /// <summary>Hex SHA-256.</summary>
    public const int LookupKeyLength = 64;

    /// <summary>Spec 6.8.5 revoke-reason width, shared with the batch.</summary>
    public const int ReasonMaxLength = 500;

    private Pin(Guid id, Guid batchId, string pinHash, string lookupKey, string prefix, string ciphertext, int maxUses)
        : base(id)
    {
        BatchId = batchId;
        PinHash = pinHash;
        LookupKey = lookupKey;
        Prefix = prefix;
        Ciphertext = ciphertext;
        MaxUses = maxUses;
        State = PinState.Unused;
    }

    // EF Core materialisation constructor.
    private Pin()
        : base()
    {
        PinHash = string.Empty;
        LookupKey = string.Empty;
        Prefix = string.Empty;
    }

    /// <summary>The batch it belongs to.</summary>
    public Guid BatchId { get; private set; }

    /// <summary>Argon2id, self-describing.</summary>
    public string PinHash { get; private set; }

    /// <summary>HMAC-SHA-256 of the normalised value under a key outside the database. Unique.</summary>
    public string LookupKey { get; private set; }

    /// <summary>The first four characters, permanently.</summary>
    public string Prefix { get; private set; }

    /// <summary>AES-256-GCM of the value, base64; null after the purge.</summary>
    public string? Ciphertext { get; private set; }

    /// <summary>Copied from the batch at generation.</summary>
    public int MaxUses { get; private set; }

    /// <summary>Incremented per spec 6.8.8 by the portal.</summary>
    public int UseCount { get; private set; }

    /// <summary>Different pupils this pin has opened; read by the spread control.</summary>
    public int DistinctPupilCount { get; private set; }

    /// <summary>Spec 6.8.5.</summary>
    public PinState State { get; private set; }

    /// <summary>Why it was suspended or revoked.</summary>
    public string? StateReason { get; private set; }

    /// <summary>Set on revocation.</summary>
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    /// <summary>Set on revocation.</summary>
    public Guid? RevokedBy { get; private set; }

    /// <summary>A freshly generated pin. The caller computed the hash, key, prefix and ciphertext.</summary>
    public static Pin Create(Guid id, Guid batchId, string pinHash, string lookupKey, string prefix, string ciphertext, int maxUses) =>
        new(id, batchId, pinHash, lookupKey, prefix, ciphertext, maxUses);

    /// <summary>Any state but Revoked to Revoked.</summary>
    public Result Revoke(string reason, Guid? revokedBy, DateTimeOffset revokedAtUtc)
    {
        if (State == PinState.Revoked)
        {
            return Result.Failure(Error.Conflict("pin.already_revoked", "This pin is already revoked."));
        }

        State = PinState.Revoked;
        StateReason = reason.Trim();
        RevokedBy = revokedBy;
        RevokedAtUtc = revokedAtUtc;
        return Result.Success();
    }

    /// <summary>
    /// Suspended back to where its uses leave it (spec 6.8.5: the only reversible state). The caller records the reason
    /// on the audit event.
    /// </summary>
    public Result Reinstate()
    {
        if (State != PinState.Suspended)
        {
            return Result.Failure(Error.Conflict("pin.not_suspended", $"This pin is {State}. Only a suspended pin can be reinstated."));
        }

        State = UseCount == 0 ? PinState.Unused : UseCount >= MaxUses ? PinState.Exhausted : PinState.Active;
        StateReason = null;
        return Result.Success();
    }

    /// <summary>Spec 6.9.3's spread control: more than this many distinct pupils in total suspends the pin.</summary>
    public const int MaxDistinctPupils = 3;

    /// <summary>Spec 6.9.3's spread control: more than this many distinct pupils within <see cref="SpreadWindow"/> suspends it.</summary>
    public const int MaxDistinctPupilsInWindow = 2;

    /// <summary>Spec 6.9.3.</summary>
    public static readonly TimeSpan SpreadWindow = TimeSpan.FromMinutes(10);

    /// <summary>Whether the pin can open a viewing session now (the portal checks the batch's session separately).</summary>
    public bool IsUsable => State is PinState.Unused or PinState.Active && UseCount < MaxUses;

    /// <summary>
    /// Counts one use (spec 6.8.8). <paramref name="isNewPupil"/> raises the distinct pupil count. The pin becomes
    /// Exhausted on its last use. The caller holds the row lock and has run the spread control.
    /// </summary>
    public void RecordUse(bool isNewPupil)
    {
        UseCount++;
        if (isNewPupil)
        {
            DistinctPupilCount++;
        }

        State = UseCount >= MaxUses ? PinState.Exhausted : PinState.Active;
    }

    /// <summary>Set by the spread control (spec 6.9.3). Reversible by <see cref="Reinstate"/>.</summary>
    public void Suspend(string reason)
    {
        if (State is PinState.Revoked or PinState.Suspended)
        {
            return;
        }

        State = PinState.Suspended;
        StateReason = reason;
    }

    /// <summary>Removes the reprint copy at the batch's purge date (spec 6.8.6).</summary>
    public void PurgeCiphertext() => Ciphertext = null;
}
