using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Pupils;

/// <summary>
/// One superseded registration number (spec 6.5.10, "Immutability and correction"): a permanent
/// lookup alias. TASK-0063 appends a row here whenever <c>pupil.regnumber.correct</c> replaces a
/// pupil's current number — "The old number goes to <c>pupil_reg_number_history</c> with the
/// reason, actor and timestamp, and stays there permanently". Rows are never updated and never
/// deleted through the application, the same append-only shape <see
/// cref="SchoolManagement.Domain.Audit.AuditEvent"/> already established for a different table — a
/// parent holding a pin slip printed with an old number must always resolve to the right child,
/// even after several corrections, so nothing here is ever superseded away.
/// </summary>
public sealed class PupilRegNumberHistory : Entity<Guid>
{
    /// <summary>Same bound as <see cref="Pupil.RegistrationNumberMaxLength"/> — every value stored here was once a live <see cref="Pupil.RegistrationNumber"/>.</summary>
    public const int OldRegistrationNumberMaxLength = Pupil.RegistrationNumberMaxLength;

    /// <summary>Spec 6.5.10: "Correction requires ... a reason of at least ten characters."</summary>
    public const int ReasonMinLength = 10;

    /// <summary>Column width for <see cref="Reason"/>, matching <c>AuditEvent.Reason</c>'s own bound for a free-text justification.</summary>
    public const int ReasonMaxLength = 500;

    private PupilRegNumberHistory(
        Guid id,
        Guid pupilId,
        string oldRegistrationNumber,
        string reason,
        Guid? correctedBy,
        DateTimeOffset correctedAtUtc)
        : base(id)
    {
        PupilId = pupilId;
        OldRegistrationNumber = oldRegistrationNumber;
        Reason = reason;
        CorrectedBy = correctedBy;
        CorrectedAtUtc = correctedAtUtc;
    }

    // EF Core materialisation constructor.
    private PupilRegNumberHistory()
        : base()
    {
        OldRegistrationNumber = null!;
        Reason = null!;
    }

    /// <summary>The pupil this alias resolves to.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>The number that was replaced. Stored verbatim, never re-formatted.</summary>
    public string OldRegistrationNumber { get; private set; }

    /// <summary>Why the correction was made. At least <see cref="ReasonMinLength"/> characters.</summary>
    public string Reason { get; private set; }

    /// <summary>The Super Admin who made the correction, or <see langword="null"/> for a genuine system action.</summary>
    public Guid? CorrectedBy { get; private set; }

    /// <summary>When the correction happened, per <see cref="TimeProvider"/> — never computed internally.</summary>
    public DateTimeOffset CorrectedAtUtc { get; private set; }

    /// <summary>
    /// Builds one row. The caller (<c>CorrectRegistrationNumberHandler</c>) has already checked the
    /// new number's own shape and the ten-character reason floor via the command validator — this
    /// re-validates the reason length anyway, the same defence-in-depth every other entity in this
    /// module applies (see <see cref="Pupil.Create"/>'s own remarks).
    /// </summary>
    /// <param name="id">A fresh <see cref="Guid.CreateVersion7()"/> value.</param>
    /// <param name="pupilId">The pupil whose number was corrected.</param>
    /// <param name="oldRegistrationNumber">The number being replaced. Never null or blank.</param>
    /// <param name="reason">At least <see cref="ReasonMinLength"/> characters.</param>
    /// <param name="correctedBy">The acting Super Admin's id, or <see langword="null"/> for a system action.</param>
    /// <param name="correctedAtUtc">"Now", injected via <see cref="TimeProvider"/>.</param>
    public static Result<PupilRegNumberHistory> Create(
        Guid id,
        Guid pupilId,
        string oldRegistrationNumber,
        string reason,
        Guid? correctedBy,
        DateTimeOffset correctedAtUtc)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<PupilRegNumberHistory>(Error.Validation(
                "pupil.regnumber_history.id_required", "Id must not be empty."));
        }

        if (pupilId == Guid.Empty)
        {
            return Result.Failure<PupilRegNumberHistory>(Error.Validation(
                "pupil.regnumber_history.pupil_id_required", "PupilId must not be empty."));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(oldRegistrationNumber);

        var trimmedReason = reason?.Trim() ?? string.Empty;

        if (trimmedReason.Length < ReasonMinLength)
        {
            return Result.Failure<PupilRegNumberHistory>(Error.Validation(
                "pupil.regnumber_correction_reason_too_short",
                $"Reason must be at least {ReasonMinLength} characters."));
        }

        if (trimmedReason.Length > ReasonMaxLength)
        {
            return Result.Failure<PupilRegNumberHistory>(Error.Validation(
                "pupil.regnumber_correction_reason_too_long",
                $"Reason must be at most {ReasonMaxLength} characters."));
        }

        return Result.Success(new PupilRegNumberHistory(
            id, pupilId, oldRegistrationNumber, trimmedReason, correctedBy, correctedAtUtc));
    }
}
