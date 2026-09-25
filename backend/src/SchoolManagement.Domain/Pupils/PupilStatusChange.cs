using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Pupils;

/// <summary>
/// One status transition made through the status screen (spec 6.5.14): who changed it, when it took effect and why.
/// Append-only, like <see cref="PupilRegNumberHistory"/>. The reason lives here rather than only on the audit event
/// because the pupil's own detail view shows it, and audit readers are a different audience.
/// </summary>
public sealed class PupilStatusChange : Entity<Guid>
{
    /// <summary>Column width for <see cref="Reason"/>, the same bound as the other free-text justifications.</summary>
    public const int ReasonMaxLength = 500;

    private PupilStatusChange(
        Guid id,
        Guid pupilId,
        PupilStatus fromStatus,
        PupilStatus toStatus,
        DateOnly effectiveDate,
        string? reason,
        Guid? armId,
        Guid? changedBy,
        DateTimeOffset changedAtUtc)
        : base(id)
    {
        PupilId = pupilId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        EffectiveDate = effectiveDate;
        Reason = reason;
        ArmId = armId;
        ChangedBy = changedBy;
        ChangedAtUtc = changedAtUtc;
    }

    // EF Core materialisation constructor.
    private PupilStatusChange()
        : base()
    {
    }

    /// <summary>The pupil whose status changed.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>The status before.</summary>
    public PupilStatus FromStatus { get; private set; }

    /// <summary>The status after.</summary>
    public PupilStatus ToStatus { get; private set; }

    /// <summary>The date the change took effect: the enrolment closed on it, or the new enrolment opened on it.</summary>
    public DateOnly EffectiveDate { get; private set; }

    /// <summary>Why. Required for every transition except a reactivation from transferred or withdrawn.</summary>
    public string? Reason { get; private set; }

    /// <summary>
    /// The arm a reactivation enrolled the pupil into, or the arm a leave took them out of (which the same-day undo
    /// reopens). <see langword="null"/> when a leave closed no enrolment.
    /// </summary>
    public Guid? ArmId { get; private set; }

    /// <summary>The acting admin, or <see langword="null"/> for a system action.</summary>
    public Guid? ChangedBy { get; private set; }

    /// <summary>When the change was recorded, per <see cref="TimeProvider"/>.</summary>
    public DateTimeOffset ChangedAtUtc { get; private set; }

    /// <summary>Builds one row. The handler has already validated the transition itself.</summary>
    public static Result<PupilStatusChange> Create(
        Guid id,
        Guid pupilId,
        PupilStatus fromStatus,
        PupilStatus toStatus,
        DateOnly effectiveDate,
        string? reason,
        Guid? armId,
        Guid? changedBy,
        DateTimeOffset changedAtUtc)
    {
        if (id == Guid.Empty || pupilId == Guid.Empty)
        {
            return Result.Failure<PupilStatusChange>(Error.Validation(
                "pupil.status_change.id_required", "Id and PupilId must not be empty."));
        }

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (trimmedReason is { Length: > ReasonMaxLength })
        {
            return Result.Failure<PupilStatusChange>(Error.Validation(
                "pupil.status_change.reason_too_long", $"Reason must be at most {ReasonMaxLength} characters."));
        }

        return Result.Success(new PupilStatusChange(
            id, pupilId, fromStatus, toStatus, effectiveDate, trimmedReason, armId, changedBy, changedAtUtc));
    }
}
