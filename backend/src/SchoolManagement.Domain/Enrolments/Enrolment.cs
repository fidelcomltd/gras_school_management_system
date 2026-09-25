using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Enrolments;

/// <summary>
/// Dated membership of a pupil in an arm (spec 02-data-model.md §5.1, §5.2). Joins one
/// <c>pupil</c> to one <c>arm</c>, with an effective-from date and an optional effective-to date.
/// </summary>
/// <remarks>
/// <para>
/// THE SINGLE MOST IMPORTANT NORMALISATION IN THE SCHEMA (spec 02 §5.2, near-verbatim): a
/// <c>pupil</c> row carries no arm or level column. Which arm a pupil is in is a query against
/// this entity for the open row — never a denormalised <c>pupil.arm_id</c> shortcut, which "will
/// break transfers, promotion and historical results at the same time." Nothing in
/// <c>SchoolManagement.Domain.Pupils</c> references an arm, and this type is the only place that
/// relationship is recorded.
/// </para>
/// <para>
/// EXACTLY ONE OPEN ENROLMENT PER PUPIL (spec 02 §5.2: "A pupil has exactly one open enrolment at
/// any moment") is enforced in the DATABASE, not only here — a partial unique index on
/// <c>(pupil_id) WHERE effective_to IS NULL</c>, see <c>EnrolmentConfiguration</c>. This entity's
/// own checks (an enrolment cannot close twice, a transfer cannot re-open the arm it just left)
/// are a second line of defence over a single row, not a substitute for that index: nothing here
/// can see a SIBLING open row for the same pupil.
/// </para>
/// <para>
/// <see cref="ArmId"/> has no setter anywhere on this type outside the private constructor.
/// Moving a pupil between arms (<see cref="Transfer"/>) never mutates it in place — it closes this
/// row and returns a brand NEW <see cref="Enrolment"/> for the destination arm, so "who sat where,
/// and from when" (spec 02 §5.2) stays reconstructable from history rather than overwritten.
/// </para>
/// </remarks>
public sealed class Enrolment : Entity<Guid>, IAuditableEntity
{
    private Enrolment(Guid id, Guid pupilId, Guid armId, DateOnly effectiveFrom)
        : base(id)
    {
        PupilId = pupilId;
        ArmId = armId;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = null;
    }

    // EF Core materialisation constructor.
    private Enrolment()
        : base()
    {
    }

    /// <summary>The enrolled pupil. Never changes once set.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>
    /// The arm this row records membership of. Never changes once set — see the type remarks on
    /// <see cref="Transfer"/>.
    /// </summary>
    public Guid ArmId { get; private set; }

    /// <summary>The date this membership began.</summary>
    public DateOnly EffectiveFrom { get; private set; }

    /// <summary>
    /// <see langword="null"/> while open. Set once, by <see cref="Close"/> — never cleared or
    /// changed afterwards, matching <c>Pupil.RegistrationNumber</c>'s "set once" convention.
    /// </summary>
    public DateOnly? EffectiveTo { get; private set; }

    /// <summary>Whether this is the pupil's current, open enrolment (spec 02 §5.2).</summary>
    public bool IsOpen => EffectiveTo is null;

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Opens a new enrolment. The caller (a future command handler, TASK-0051's admission approval
    /// among others) must already have checked that <paramref name="pupilId"/> has no other open
    /// enrolment — this entity cannot see sibling rows, so that invariant is the database partial
    /// unique index's job, backstopped here only by rejecting an obviously-empty id.
    /// </summary>
    /// <param name="id">A fresh <see cref="Guid.CreateVersion7()"/> value.</param>
    /// <param name="pupilId">The pupil being enrolled.</param>
    /// <param name="armId">The destination arm.</param>
    /// <param name="effectiveFrom">
    /// Spec 07 §6.5.11: "the admission date or the session start date, whichever is later" for the
    /// admission-approval case; the transfer date for <see cref="Transfer"/>. Not validated against
    /// "today" here — the caller resolves which date rule applies.
    /// </param>
    public static Result<Enrolment> Open(Guid id, Guid pupilId, Guid armId, DateOnly effectiveFrom)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Enrolment>(Error.Validation("enrolment.id_required", "Id must not be empty."));
        }

        if (pupilId == Guid.Empty)
        {
            return Result.Failure<Enrolment>(Error.Validation("enrolment.pupil_id_required", "PupilId must not be empty."));
        }

        if (armId == Guid.Empty)
        {
            return Result.Failure<Enrolment>(Error.Validation("enrolment.arm_id_required", "ArmId must not be empty."));
        }

        return Result.Success(new Enrolment(id, pupilId, armId, effectiveFrom));
    }

    /// <summary>
    /// Closes this enrolment (spec 07 §6.5.14: transfer, withdrawal or graduation all close the
    /// open enrolment on their effective date). The caller's job: deciding WHICH status transition
    /// justifies the close, and persisting it.
    /// </summary>
    /// <param name="effectiveTo">Must not precede <see cref="EffectiveFrom"/>.</param>
    public Result Close(DateOnly effectiveTo)
    {
        if (!IsOpen)
        {
            return Result.Failure(Error.Conflict(
                "enrolment.already_closed", "This enrolment is already closed."));
        }

        if (effectiveTo < EffectiveFrom)
        {
            return Result.Failure(Error.Validation(
                "enrolment.effective_to_before_effective_from",
                $"The effective-to date cannot be before this enrolment's effective-from date of {EffectiveFrom:dd/MM/yyyy}."));
        }

        EffectiveTo = effectiveTo;
        return Result.Success();
    }

    /// <summary>
    /// Moves a pupil from their current open enrolment to a different arm (spec 02 §5.2: "Moving a
    /// pupil from Primary 2A to Primary 2C closes the first enrolment on the transfer date and
    /// opens a second"). Closes <paramref name="currentOpenEnrolment"/> in place and returns a
    /// brand new <see cref="Enrolment"/> for <paramref name="destinationArmId"/> — <see cref="ArmId"/>
    /// is never reassigned on the existing row, which is what keeps the history real.
    /// </summary>
    /// <param name="currentOpenEnrolment">The pupil's open enrolment, tracked, about to be closed.</param>
    /// <param name="newEnrolmentId">A fresh <see cref="Guid.CreateVersion7()"/> value for the new row.</param>
    /// <param name="destinationArmId">The arm the pupil is moving to. Must differ from the current arm.</param>
    /// <param name="transferDate">
    /// Opens the new row on this date and closes the old one the day before (spec 06 §6.4.4 step 3: "effective_to set to
    /// the day before"), so no date belongs to two arms. Must therefore fall after the old row's effective-from date.
    /// </param>
    public static Result<Enrolment> Transfer(
        Enrolment currentOpenEnrolment,
        Guid newEnrolmentId,
        Guid destinationArmId,
        DateOnly transferDate)
    {
        ArgumentNullException.ThrowIfNull(currentOpenEnrolment);

        if (destinationArmId == currentOpenEnrolment.ArmId)
        {
            return Result.Failure<Enrolment>(Error.Validation(
                "enrolment.transfer_destination_is_current_arm",
                "The pupil is already enrolled in this arm."));
        }

        if (currentOpenEnrolment.IsOpen && transferDate <= currentOpenEnrolment.EffectiveFrom)
        {
            return Result.Failure<Enrolment>(Error.Validation(
                "enrolment.transfer_date_not_after_start",
                $"A move must take effect after {currentOpenEnrolment.EffectiveFrom:dd/MM/yyyy}, the date the pupil joined this arm."));
        }

        var closeResult = currentOpenEnrolment.Close(transferDate.AddDays(-1));

        if (closeResult.IsFailure)
        {
            return Result.Failure<Enrolment>(closeResult.Error);
        }

        return Open(newEnrolmentId, currentOpenEnrolment.PupilId, destinationArmId, transferDate);
    }
}
