namespace SchoolManagement.Application.Results;

/// <summary>
/// Whether a mark cell has every part filled, some, or none (TASK-0088 stage B; spec §6.7.4, §6.7.5).
/// A blank part is never a zero — <see cref="Empty"/> and <see cref="Partial"/> both mean "at least
/// one part still unfilled", distinguished only by whether ANY part has been.
/// </summary>
public enum MarkCompletionStatus
{
    /// <summary>Every non-exam component has a mark, and the exam has a mark or is marked absent.</summary>
    Complete,

    /// <summary>At least one part is filled, at least one is not.</summary>
    Partial,

    /// <summary>No mark row exists for this pupil and subject, or every part is blank. A voided row also reads as this.</summary>
    Empty,
}

/// <summary>One subject column on the readiness grid (TASK-0088 stage B), in §8.1's resolver order.</summary>
/// <param name="SubjectId">The subject's id.</param>
/// <param name="Name">The subject's display name.</param>
public sealed record ReadinessSubjectDto(string SubjectId, string Name);

/// <summary>One pupil's mark cell for one subject (TASK-0088 stage B) — parallel to <see cref="ReadinessSubjectDto"/> in <see cref="ReadinessPupilRowDto.Marks"/>.</summary>
/// <param name="SubjectId">Which subject column this cell belongs to.</param>
/// <param name="Status">Complete, Partial or Empty.</param>
/// <param name="FilledParts">How many of the sheet's <c>componentCount</c> parts are filled.</param>
public sealed record ReadinessMarkCellDto(string SubjectId, MarkCompletionStatus Status, int FilledParts);

/// <summary>One pupil's row on the readiness grid (TASK-0088 stage B) — every active pupil in the arm, surname order.</summary>
/// <param name="PupilId">The pupil's id.</param>
/// <param name="RegistrationNumber"><see langword="null"/> only if somehow unissued.</param>
/// <param name="DisplayName">Composed "Surname First Middle", same convention as <c>ScoreSheetRowDto</c>.</param>
/// <param name="Marks">One cell per subject in the sheet's <c>subjects</c> list, same order.</param>
/// <param name="RatingsComplete">
/// R1-A: every active trait rated (when the arm's section rates traits) or every active development
/// indicator of the section's active domains rated (otherwise). Indicator comments are never required.
/// </param>
/// <param name="AttendanceComplete"><c>timesPresent</c> is set, the term's <c>timesSchoolOpened</c> is set, and present is at most opened (2026-09-19 race drift).</param>
/// <param name="ClassTeacherRemarkPresent">Whether a class-teacher remark row exists for this pupil.</param>
/// <param name="HeadTeacherRemarkPresent">Whether a head-teacher remark row exists for this pupil — informational only, never a submission blocker.</param>
public sealed record ReadinessPupilRowDto(
    string PupilId,
    string? RegistrationNumber,
    string DisplayName,
    IReadOnlyList<ReadinessMarkCellDto> Marks,
    bool RatingsComplete,
    bool AttendanceComplete,
    bool ClassTeacherRemarkPresent,
    bool HeadTeacherRemarkPresent);

/// <summary>One pupil excluded from the gate because their enrolment in this arm closed during the term (TASK-0088 stage B, AC B4).</summary>
/// <param name="PupilId">The pupil's id.</param>
/// <param name="RegistrationNumber"><see langword="null"/> only if somehow unissued.</param>
/// <param name="DisplayName">Composed "Surname First Middle".</param>
/// <param name="LeftOn">The enrolment's <c>EffectiveTo</c> date.</param>
public sealed record ReadinessLeftDuringTermPupilDto(string PupilId, string? RegistrationNumber, string DisplayName, DateOnly LeftOn);

/// <summary>One readiness counter (TASK-0088 stage B) — cells for marks, pupils for everything else.</summary>
/// <param name="Complete">How many are complete.</param>
/// <param name="Total">How many there are altogether.</param>
public sealed record ReadinessCounterDto(int Complete, int Total);

/// <summary>The five counters spec §6.7.5 names, plus the head teacher's remark as an informational fifth (TASK-0088 human ruling).</summary>
/// <param name="Marks">Cells: pupils times subjects.</param>
/// <param name="Ratings">Pupils.</param>
/// <param name="Attendance">Pupils.</param>
/// <param name="ClassTeacherRemarks">Pupils.</param>
/// <param name="HeadTeacherRemarks">Pupils — informational, never a blocker.</param>
public sealed record ReadinessCountersDto(
    ReadinessCounterDto Marks,
    ReadinessCounterDto Ratings,
    ReadinessCounterDto Attendance,
    ReadinessCounterDto ClassTeacherRemarks,
    ReadinessCounterDto HeadTeacherRemarks);

/// <summary>One reason submission is blocked (TASK-0088 stage B, AC B3), in the fixed order the card names.</summary>
/// <param name="Code">One of the eight stable codes AC B3 lists.</param>
/// <param name="Message">Human-readable, reusing spec §6.7.5/§6.7.12 wording where one exists.</param>
public sealed record ReadinessBlockerDto(string Code, string Message);

/// <summary>
/// The arm's readiness grid for one term (TASK-0088 stage B; spec §6.7.5, §6.7.11) — both what
/// <c>GET /arms/{armId}/readiness</c> returns and what <c>POST /result-sets/{id}/submit</c>'s 422
/// carries, so the screen never needs a second call for the same information.
/// </summary>
/// <param name="ArmId">The arm this grid belongs to.</param>
/// <param name="TermId">The term this grid is for.</param>
/// <param name="ResultSet"><see langword="null"/> for a "Not started" arm — everything is then missing.</param>
/// <param name="Subjects">The subjects in effect, in §8.1 resolver order.</param>
/// <param name="ComponentCount">Parts per mark cell: the non-exam components plus the exam.</param>
/// <param name="Pupils">Every active pupil in the arm, surname order.</param>
/// <param name="LeftDuringTerm">Pupils whose enrolment in this arm closed inside the term's dates (AC B4) — excluded from every counter and from <paramref name="Pupils"/>.</param>
/// <param name="Counters">The five completeness counters.</param>
/// <param name="Blockers">Why submission is blocked, in a fixed order. Empty when nothing blocks it.</param>
/// <param name="CanSubmit">True iff <paramref name="Blockers"/> is empty and the set is Draft or Returned for Correction.</param>
public sealed record ResultSetReadinessDto(
    string ArmId,
    string TermId,
    ResultSetSummaryDto? ResultSet,
    IReadOnlyList<ReadinessSubjectDto> Subjects,
    int ComponentCount,
    IReadOnlyList<ReadinessPupilRowDto> Pupils,
    IReadOnlyList<ReadinessLeftDuringTermPupilDto> LeftDuringTerm,
    ReadinessCountersDto Counters,
    IReadOnlyList<ReadinessBlockerDto> Blockers,
    bool CanSubmit);
