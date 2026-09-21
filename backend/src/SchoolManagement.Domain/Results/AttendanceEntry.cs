using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Results;

/// <summary>
/// One pupil's times-present count, for one result set (spec 09 §6.7.3's <c>attendance_entry</c>;
/// appendix C.5; TASK-0086 stage A, ruling A). Stores <see cref="TimesPresent"/> ONLY — the sheet's
/// times-absent is <c>term.times_school_opened</c> minus <see cref="TimesPresent"/>, derived on
/// read (ruling A, 2026-09-19), never stored, so the two figures can never disagree (appendix C.5:
/// "Never typed, so the three figures cannot disagree").
/// </summary>
/// <remarks>
/// UNIQUE ON <c>(result_set_id, pupil_id)</c> — one attendance row per pupil per result set. A
/// cleared value (an explicit <see langword="null"/> <c>timesPresent</c> on save) is a row DELETE,
/// same convention <see cref="TraitRating"/> uses for an explicit-null cell.
/// </remarks>
public sealed class AttendanceEntry : Entity<Guid>, IAuditableEntity
{
    /// <summary>Present cannot be negative. There is no fixed upper bound here — the upper bound is data-dependent on the term's <c>times_school_opened</c> and is the caller's job (spec §6.7.7 amendment).</summary>
    public const int MinTimesPresent = 0;

    private AttendanceEntry(Guid id, Guid resultSetId, Guid pupilId, int timesPresent)
        : base(id)
    {
        ResultSetId = resultSetId;
        PupilId = pupilId;
        TimesPresent = timesPresent;
    }

    // EF Core materialisation constructor.
    private AttendanceEntry()
        : base()
    {
    }

    /// <summary>The result set this entry belongs to.</summary>
    public Guid ResultSetId { get; private set; }

    /// <summary>Unique within <see cref="ResultSetId"/>.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>Times the pupil was present. The upper bound against the term's times-school-opened is the caller's job.</summary>
    public int TimesPresent { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>Creates a new attendance row. The caller has already validated every rule — this entity trusts its input, same posture as <see cref="TraitRating.Create"/>.</summary>
    public static Result<AttendanceEntry> Create(Guid id, Guid resultSetId, Guid pupilId, int timesPresent)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<AttendanceEntry>(Error.Validation("attendance_entry.id_required", "Id must not be empty."));
        }

        if (resultSetId == Guid.Empty || pupilId == Guid.Empty)
        {
            return Result.Failure<AttendanceEntry>(Error.Validation(
                "attendance_entry.reference_required", "ResultSetId and PupilId must not be empty."));
        }

        if (timesPresent < MinTimesPresent)
        {
            return Result.Failure<AttendanceEntry>(Error.Validation(
                "attendance_entry.times_present_out_of_range", "Times present must not be negative."));
        }

        return Result.Success(new AttendanceEntry(id, resultSetId, pupilId, timesPresent));
    }

    /// <summary>Applies a new value to an EXISTING row. The caller has already validated it.</summary>
    public void UpdateTimesPresent(int timesPresent) => TimesPresent = timesPresent;
}
