using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>One assessment-structure column on the sheet (spec 6.7.4): a non-examination component, or the examination itself.</summary>
/// <param name="Id">The assessment component's id — also the key <see cref="ScoreSheetRowDto.ComponentMarks"/> uses.</param>
/// <param name="Label">The component's short label, for a tight column header (for example "CA1").</param>
/// <param name="MaxMark">The component's maximum mark, from settings. Never a literal.</param>
public sealed record ScoreSheetComponentDto(string Id, string Label, int MaxMark);

/// <summary>The arm's result set as it stands, or <see langword="null"/> when none exists yet ("Not started").</summary>
/// <param name="Id">The result set's id.</param>
/// <param name="State">Spec 6.7.11's six-member state machine.</param>
/// <param name="NeedsRecompute">Whether the computed rows are stale.</param>
public sealed record ResultSetSummaryDto(string Id, ResultSetState State, bool NeedsRecompute);

/// <summary>One pupil's row on the sheet (spec 6.7.4) — every active pupil in the arm, including one with no marks entered at all.</summary>
/// <param name="PupilId">The pupil's id.</param>
/// <param name="RegistrationNumber"><see langword="null"/> only if somehow unissued.</param>
/// <param name="DisplayName">Composed "Surname First Middle", for the row identity alongside <paramref name="RegistrationNumber"/>.</param>
/// <param name="ComponentMarks">Component id to mark, <see langword="null"/> for a blank cell — never an implicit zero.</param>
/// <param name="ExamMark"><see langword="null"/> when blank or when <paramref name="ExamAbsent"/> is true.</param>
/// <param name="ExamAbsent">True when the pupil did not sit the examination.</param>
/// <param name="CaTotal">Sum of the non-examination components. <see langword="null"/> unless every one of them is filled.</param>
/// <param name="SubjectTotal"><paramref name="CaTotal"/> plus the exam mark (or <paramref name="CaTotal"/> alone if absent). <see langword="null"/> unless the row is complete.</param>
public sealed record ScoreSheetRowDto(
    string PupilId,
    string? RegistrationNumber,
    string DisplayName,
    IReadOnlyDictionary<string, int?> ComponentMarks,
    int? ExamMark,
    bool ExamAbsent,
    int? CaTotal,
    int? SubjectTotal);

/// <summary>
/// One arm's score sheet for one subject and term (spec 6.7.4; TASK-0076's approved contract delta).
/// Departs from spec 6.7.13's <c>?arm_id=</c> query form — see the endpoint's own remarks.
/// </summary>
/// <param name="ArmId">The arm this sheet belongs to.</param>
/// <param name="SubjectId">The subject this sheet is for.</param>
/// <param name="TermId">The term this sheet is for.</param>
/// <param name="Version">
/// Opaque, derived from the rows the sheet covers (see <c>ScoreSheetVersion</c>).
/// <see langword="null"/> before any row exists. Send back unchanged on <c>PUT</c> to detect a
/// concurrent edit.
/// </param>
/// <param name="ResultSet"><see langword="null"/> until the first save creates one ("Not started").</param>
/// <param name="Components">Non-examination components only, in settings order.</param>
/// <param name="Examination">The examination column — always last, never in <paramref name="Components"/>.</param>
/// <param name="Rows">Every active pupil in the arm, surname then id — fixed, never affected by marks.</param>
public sealed record ScoreSheetDto(
    string ArmId,
    string SubjectId,
    string TermId,
    string? Version,
    ResultSetSummaryDto? ResultSet,
    IReadOnlyList<ScoreSheetComponentDto> Components,
    ScoreSheetComponentDto Examination,
    IReadOnlyList<ScoreSheetRowDto> Rows);
