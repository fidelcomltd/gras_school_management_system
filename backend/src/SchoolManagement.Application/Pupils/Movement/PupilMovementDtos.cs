using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Pupils.Movement;

/// <summary>What a status change or transfer does to one result set (spec 06 §6.4.4 steps 5 and 6).</summary>
public enum PupilMovementEffect
{
    /// <summary>Published for the term the move falls in (or a later one): the transfer is refused until it is withdrawn.</summary>
    Blocks,

    /// <summary>Gains <c>needs_recompute</c> and keeps its state.</summary>
    NeedsRecompute,

    /// <summary>Gains <c>needs_recompute</c> and drops back to Draft (it was Awaiting Approval or Returned for Correction).</summary>
    RevertsToDraft,
}

/// <summary>One result set a move touches.</summary>
/// <param name="ResultSetId">The set.</param>
/// <param name="ArmId">Its arm.</param>
/// <param name="ArmName">Composed display name, for example Primary 2A.</param>
/// <param name="TermId">Its term.</param>
/// <param name="TermName">For example First Term.</param>
/// <param name="State">Its state before the move.</param>
/// <param name="Effect">What the move does to it.</param>
public sealed record PupilMovementResultSetDto(
    string ResultSetId,
    string ArmId,
    string ArmName,
    string TermId,
    string TermName,
    ResultSetState State,
    PupilMovementEffect Effect);

/// <summary>The destination arm's capacity once this pupil is added (spec 6.4.6). A soft limit.</summary>
/// <param name="Capacity">The arm's capacity.</param>
/// <param name="EnrolledAfter">Open enrolments once this pupil is added.</param>
/// <param name="OverCapacity">Whether this pupil takes the arm past its capacity.</param>
/// <param name="CanOverride">Whether the caller holds <c>arm.capacity.override</c> for the arm; without it an over-capacity move is refused.</param>
public sealed record PupilMovementCapacityDto(int Capacity, int EnrolledAfter, bool OverCapacity, bool CanOverride);

/// <summary>
/// The outcome of <c>POST /pupils/{id}/status</c> or <c>POST /pupils/{id}/transfer</c>. With <c>dryRun</c> nothing is
/// written and <see cref="Pupil"/> is the pupil as it stands; otherwise the change has been made.
/// </summary>
/// <param name="DryRun">Echoes the request.</param>
/// <param name="Pupil">The pupil, after the change unless <paramref name="DryRun"/>.</param>
/// <param name="FromStatus">The status before.</param>
/// <param name="ToStatus">The status after (the same for a transfer).</param>
/// <param name="FromArmId">The arm the pupil leaves; <see langword="null"/> when they had no open enrolment.</param>
/// <param name="FromArmName">Its display name.</param>
/// <param name="ToArmId">The arm the pupil joins; <see langword="null"/> when they leave the school.</param>
/// <param name="ToArmName">Its display name.</param>
/// <param name="EffectiveDate">When the change takes effect.</param>
/// <param name="EnrolmentClosesOn">The date the old enrolment closes: the effective date, or the day before it for a transfer.</param>
/// <param name="ResultSets">Every result set the move touches, and how. A <see cref="PupilMovementEffect.Blocks"/> entry means a real call is refused.</param>
/// <param name="Capacity">The destination's capacity; <see langword="null"/> when the pupil joins no arm.</param>
public sealed record PupilMovementOutcomeDto(
    bool DryRun,
    PupilDto Pupil,
    PupilStatus FromStatus,
    PupilStatus ToStatus,
    string? FromArmId,
    string? FromArmName,
    string? ToArmId,
    string? ToArmName,
    DateOnly EffectiveDate,
    DateOnly? EnrolmentClosesOn,
    IReadOnlyList<PupilMovementResultSetDto> ResultSets,
    PupilMovementCapacityDto? Capacity);

/// <summary>One enrolment in the pupil's history (spec 02 §5.2).</summary>
/// <param name="ArmId">The arm.</param>
/// <param name="ArmName">Its composed display name.</param>
/// <param name="SessionId">The arm's session.</param>
/// <param name="SessionName">For example 2026/2027.</param>
/// <param name="EffectiveFrom">The first day in the arm.</param>
/// <param name="EffectiveTo">The last day; <see langword="null"/> while open.</param>
public sealed record PupilEnrolmentDto(
    string ArmId, string ArmName, string SessionId, string SessionName, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

/// <summary>One status-screen transition (spec 6.5.14).</summary>
/// <param name="FromStatus">Before.</param>
/// <param name="ToStatus">After.</param>
/// <param name="EffectiveDate">When it took effect.</param>
/// <param name="Reason">Why; <see langword="null"/> for a reactivation given none.</param>
/// <param name="ArmName">The arm a reactivation enrolled the pupil into.</param>
/// <param name="ChangedAtUtc">When it was recorded.</param>
public sealed record PupilStatusChangeDto(
    PupilStatus FromStatus, PupilStatus ToStatus, DateOnly EffectiveDate, string? Reason, string? ArmName, DateTimeOffset ChangedAtUtc);

/// <summary><c>GET /pupils/{id}/enrolments</c>: where the pupil sits now, where they sat before, and every status change.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="Status">The pupil's current status.</param>
/// <param name="CurrentArmId">The open enrolment's arm; <see langword="null"/> when the pupil has none.</param>
/// <param name="CurrentArmName">Its display name.</param>
/// <param name="Enrolments">Oldest first.</param>
/// <param name="StatusChanges">Oldest first.</param>
public sealed record PupilEnrolmentHistoryDto(
    string PupilId,
    PupilStatus Status,
    string? CurrentArmId,
    string? CurrentArmName,
    IReadOnlyList<PupilEnrolmentDto> Enrolments,
    IReadOnlyList<PupilStatusChangeDto> StatusChanges);
