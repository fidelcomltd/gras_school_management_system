using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Pins;

/// <summary>Spec 6.8.10's batch states.</summary>
public enum PinBatchState
{
    /// <summary>Pins exist, plaintext exists, nothing printed.</summary>
    Generated,

    /// <summary>The print view has been rendered at least once.</summary>
    Printed,

    /// <summary>Marked distributed; in circulation.</summary>
    Active,

    /// <summary>Every pin used up, suspended or revoked, or the session closed. Cosmetic.</summary>
    Exhausted,

    /// <summary>Killed with a reason. Terminal; every pin in it is revoked.</summary>
    Revoked,
}

/// <summary>
/// A batch of access pins (spec 6.8.4): the unit of generation, printing, revocation and reporting. Pins are not
/// tied to a pupil (6.8.2, reconfirmed by the human 2026-09-22), so a batch has no arm and no roster.
/// </summary>
public sealed class PinBatch : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.8.4 column width.</summary>
    public const int NameMaxLength = 80;

    /// <summary>Spec 6.8.4 column width.</summary>
    public const int PurposeNoteMaxLength = 200;

    /// <summary>Spec 6.8.6: the length is never reduced below 10.</summary>
    public const int MinPinLength = 10;

    /// <summary>Spec 6.8.4.</summary>
    public const int MaxPinLength = 16;

    /// <summary>Spec 6.8.4 default, from settings once that screen exists.</summary>
    public const int DefaultPinLength = 10;

    /// <summary>Spec 6.8.4 bounds and default; low on purpose (6.8.2).</summary>
    public const int MinMaxUses = 1;

    /// <summary>Spec 6.8.4.</summary>
    public const int MaxMaxUses = 100;

    /// <summary>Spec 6.8.4.</summary>
    public const int DefaultMaxUses = 3;

    /// <summary>Spec 6.8.12: above this, generation needs the number typed back.</summary>
    public const int MaxUsesConfirmationThreshold = 10;

    /// <summary>Spec 6.8.4.</summary>
    public const int MaxPinCount = 2000;

    /// <summary>Spec 6.8.4: ciphertext is purged 30 days after generation.</summary>
    public static readonly TimeSpan PlaintextRetention = TimeSpan.FromDays(30);

    private PinBatch(Guid id, Guid sessionId, string name, string? purposeNote, int pinLength, int maxUses, int pinCount, DateTimeOffset generatedAtUtc, Guid? generatedBy)
        : base(id)
    {
        SessionId = sessionId;
        Name = name;
        PurposeNote = purposeNote;
        PinLength = pinLength;
        MaxUses = maxUses;
        PinCount = pinCount;
        State = PinBatchState.Generated;
        GeneratedAtUtc = generatedAtUtc;
        GeneratedBy = generatedBy;
        PlaintextPurgeAtUtc = generatedAtUtc + PlaintextRetention;
    }

    // EF Core materialisation constructor.
    private PinBatch()
        : base()
    {
        Name = string.Empty;
    }

    /// <summary>The session the pins are valid for.</summary>
    public Guid SessionId { get; private set; }

    /// <summary>Unique within the session.</summary>
    public string Name { get; private set; }

    /// <summary>Informational only; it restricts nothing.</summary>
    public string? PurposeNote { get; private set; }

    /// <summary>Fixed at generation.</summary>
    public int PinLength { get; private set; }

    /// <summary>Fixed at generation and copied to every pin.</summary>
    public int MaxUses { get; private set; }

    /// <summary>How many pins were generated.</summary>
    public int PinCount { get; private set; }

    /// <summary>Spec 6.8.10.</summary>
    public PinBatchState State { get; private set; }

    /// <summary>After this the pins' ciphertext is purged and the batch cannot be reprinted.</summary>
    public DateTimeOffset PlaintextPurgeAtUtc { get; private set; }

    /// <summary>When generated.</summary>
    public DateTimeOffset GeneratedAtUtc { get; private set; }

    /// <summary>Who generated it.</summary>
    public Guid? GeneratedBy { get; private set; }

    /// <summary>Set on revocation.</summary>
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    /// <summary>Set on revocation.</summary>
    public Guid? RevokedBy { get; private set; }

    /// <summary>Required on revocation; 500 characters.</summary>
    public string? RevokeReason { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>Creates a batch in <see cref="PinBatchState.Generated"/>. The caller validated the bounds.</summary>
    public static PinBatch Create(
        Guid id, Guid sessionId, string name, string? purposeNote, int pinLength, int maxUses, int pinCount, DateTimeOffset generatedAtUtc, Guid? generatedBy) =>
        new(id, sessionId, name.Trim(), string.IsNullOrWhiteSpace(purposeNote) ? null : purposeNote.Trim(), pinLength, maxUses, pinCount, generatedAtUtc, generatedBy);

    /// <summary>Generated to Printed when the print view is rendered (6.8.9 step 6). Later states are untouched.</summary>
    public void MarkPrinted()
    {
        if (State == PinBatchState.Generated)
        {
            State = PinBatchState.Printed;
        }
    }

    /// <summary>Generated or Printed to Active (6.8.9 step 8).</summary>
    public Result MarkDistributed()
    {
        if (State is not (PinBatchState.Generated or PinBatchState.Printed))
        {
            return Result.Failure(Error.Conflict("pin_batch.not_distributable", $"This batch is {State} and cannot be marked distributed."));
        }

        State = PinBatchState.Active;
        return Result.Success();
    }

    /// <summary>Any state but Revoked to Revoked. The caller revokes every pin in the same transaction.</summary>
    public Result Revoke(string reason, Guid? revokedBy, DateTimeOffset revokedAtUtc)
    {
        if (State == PinBatchState.Revoked)
        {
            return Result.Failure(Error.Conflict("pin_batch.already_revoked", "This batch is already revoked."));
        }

        State = PinBatchState.Revoked;
        RevokeReason = reason.Trim();
        RevokedBy = revokedBy;
        RevokedAtUtc = revokedAtUtc;
        return Result.Success();
    }
}
