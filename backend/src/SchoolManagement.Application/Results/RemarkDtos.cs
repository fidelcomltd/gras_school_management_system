namespace SchoolManagement.Application.Results;

/// <summary>
/// One pupil's row on a remark sheet (TASK-0086 stage A) — every active pupil in the arm, including
/// one with no remark entered at all. Shared shape for both the class-teacher and head-teacher
/// remark sheets (appendix C.6); which sheet a given <see cref="RemarkSheetDto"/> is belongs to is
/// implied by which endpoint returned it, not carried as a field.
/// </summary>
/// <param name="PupilId">The pupil's id.</param>
/// <param name="RegistrationNumber"><see langword="null"/> only if somehow unissued.</param>
/// <param name="DisplayName">Composed "Surname First Middle", same convention as <see cref="TraitRatingRowDto"/>.</param>
/// <param name="Remark"><see langword="null"/> when nothing has been written for this pupil.</param>
/// <param name="WrittenByName">The staff-name snapshot captured when the text last changed (appendix C.6). <see langword="null"/> alongside <paramref name="Remark"/>.</param>
/// <param name="WrittenAt">When the text last changed. <see langword="null"/> alongside <paramref name="Remark"/>.</param>
public sealed record RemarkRowDto(
    string PupilId, string? RegistrationNumber, string DisplayName, string? Remark, string? WrittenByName, DateTimeOffset? WrittenAt);

/// <summary>
/// One arm's remark sheet (class-teacher's or head-teacher's) for one term (TASK-0086 stage A;
/// spec §6.7.7). Arm-scoped rather than result-set-scoped, mirroring <see cref="TraitRatingSheetDto"/>.
/// </summary>
/// <param name="ArmId">The arm this sheet belongs to.</param>
/// <param name="TermId">The term this sheet is for.</param>
/// <param name="Version">
/// Opaque, derived from the remarks this sheet covers — the class-teacher and head-teacher sheets
/// version INDEPENDENTLY, each over its own <c>RemarkKind</c> slice only. <see langword="null"/>
/// before any remark of this kind exists. Send back unchanged on <c>PUT</c> to detect a concurrent
/// edit.
/// </param>
/// <param name="ResultSet"><see langword="null"/> until the first save creates one ("Not started").</param>
/// <param name="Rows">Every active pupil in the arm, surname then id — fixed, never affected by remarks.</param>
public sealed record RemarkSheetDto(
    string ArmId,
    string TermId,
    string? Version,
    ResultSetSummaryDto? ResultSet,
    IReadOnlyList<RemarkRowDto> Rows);
