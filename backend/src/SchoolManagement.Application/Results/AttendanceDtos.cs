namespace SchoolManagement.Application.Results;

/// <summary>One pupil's row on the attendance sheet (TASK-0086 stage A) — every active pupil in the arm, including one with no attendance entered at all.</summary>
/// <param name="PupilId">The pupil's id.</param>
/// <param name="RegistrationNumber"><see langword="null"/> only if somehow unissued.</param>
/// <param name="DisplayName">Composed "Surname First Middle", same convention as <see cref="TraitRatingRowDto"/>.</param>
/// <param name="TimesPresent"><see langword="null"/> when nothing has been entered for this pupil.</param>
/// <param name="TimesAbsent">
/// Derived: <c>timesSchoolOpened - timesPresent</c> (ruling A, 2026-09-19). <see langword="null"/>
/// when either side is unknown — <paramref name="TimesPresent"/> is null, or the term's
/// <c>timesSchoolOpened</c> is not yet set.
/// </param>
public sealed record AttendanceRowDto(
    string PupilId, string? RegistrationNumber, string DisplayName, int? TimesPresent, int? TimesAbsent);

/// <summary>
/// One arm's attendance sheet for one term (TASK-0086 stage A; spec §6.7.7, §6.7.12 amendment —
/// entered for a nursery arm too, even though it is not printed there). Arm-scoped rather than
/// result-set-scoped, mirroring <see cref="TraitRatingSheetDto"/>.
/// </summary>
/// <param name="ArmId">The arm this sheet belongs to.</param>
/// <param name="TermId">The term this sheet is for.</param>
/// <param name="Version">
/// Opaque, derived from the entries the sheet covers. <see langword="null"/> before any entry
/// exists. Send back unchanged on <c>PUT</c> to detect a concurrent edit.
/// </param>
/// <param name="ResultSet"><see langword="null"/> until the first save creates one ("Not started").</param>
/// <param name="TimesSchoolOpened">The term's own value (not per pupil). <see langword="null"/> while blank.</param>
/// <param name="Rows">Every active pupil in the arm, surname then id — fixed, never affected by attendance.</param>
public sealed record AttendanceSheetDto(
    string ArmId,
    string TermId,
    string? Version,
    ResultSetSummaryDto? ResultSet,
    int? TimesSchoolOpened,
    IReadOnlyList<AttendanceRowDto> Rows);
