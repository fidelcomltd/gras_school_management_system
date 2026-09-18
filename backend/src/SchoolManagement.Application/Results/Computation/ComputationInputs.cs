using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Results.Computation;

/// <summary>One assessment component the engine needs to know the shape of (spec 8.1, 8.3).</summary>
/// <param name="ComponentId">Matches a key in <see cref="ComputationMarkRow.ComponentMarks"/>.</param>
/// <param name="IsExamination">True for the single examination component; false for every CA component.</param>
public sealed record ComputationComponent(Guid ComponentId, bool IsExamination);

/// <summary>One subject in effect for the arm this term (spec 8.1's subject input, already resolved by <c>SubjectsInEffectResolver</c>).</summary>
/// <param name="SubjectId">The subject's id.</param>
/// <param name="SubjectName">For the grading-band-not-found message (spec 6.7.6).</param>
public sealed record ComputationSubject(Guid SubjectId, string SubjectName);

/// <summary>One pupil on the arm's active roster (spec 8.1).</summary>
/// <param name="PupilId">The pupil's id.</param>
/// <param name="RegistrationNumber">For the grading-band-not-found message (spec 6.7.6). Never null for an active pupil in practice.</param>
/// <param name="Surname">Tie-break key for <see cref="TieBreakRule.ExamThenAlphabetical"/>.</param>
public sealed record ComputationPupil(Guid PupilId, string? RegistrationNumber, string Surname);

/// <summary>
/// One pupil's marks for one subject (spec 8.1) — present here only when a <c>subject_score</c> row
/// exists (non-voided). A pupil/subject pair with NO row at all ("missing", human ruling 2) simply has
/// no <see cref="ComputationMarkRow"/> in the input; there is no sentinel for it.
/// </summary>
/// <param name="PupilId">The pupil this row belongs to.</param>
/// <param name="SubjectId">The subject this row belongs to.</param>
/// <param name="ComponentMarks">Every non-examination component id present in the input structure MAY be a key; a missing key or a null value both mean blank (human ruling 2: "incomplete").</param>
/// <param name="ExamMark">Null when blank or when <paramref name="ExamAbsent"/> is true.</param>
/// <param name="ExamAbsent">True means the pupil did not sit the examination.</param>
public sealed record ComputationMarkRow(
    Guid PupilId,
    Guid SubjectId,
    IReadOnlyDictionary<Guid, int?> ComponentMarks,
    int? ExamMark,
    bool ExamAbsent);

/// <summary>One grading band (spec 6.2.5/8.3). <see cref="LowerBound"/> and <see cref="UpperBound"/> are inclusive.</summary>
public sealed record ComputationGradingBand(int LowerBound, int UpperBound, string GradeLetter, string Remark);

/// <summary>The subset of the result-rules singleton (spec 6.2.8) the engine reads.</summary>
/// <param name="TieBreakRule">Spec 6.7.6's tie-breaking table.</param>
/// <param name="PassMark">0-100. <c>is_pass</c> threshold (spec 6.7.6).</param>
/// <param name="MinSubjectsForPosition">Spec 6.7.6: excludes a pupil with fewer SCORED (complete) subjects than this from ranking, and from the rank denominator.</param>
public sealed record ComputationRules(TieBreakRule TieBreakRule, int PassMark, int MinSubjectsForPosition);

/// <summary>
/// One arm's roster, subject set and marks (spec 8.1) — the shape both "this" result set's own arm and
/// a sibling arm read for human ruling 1's level-position ranking use identically.
/// </summary>
/// <param name="ArmId">The arm this data belongs to.</param>
/// <param name="Pupils">The arm's active roster (spec 8.1: "Pupils with an open enrolment in the arm, status active, as at the moment computation runs").</param>
/// <param name="Subjects">The subjects in effect for this arm this term (spec 8.1, via <c>SubjectsInEffectResolver</c>).</param>
/// <param name="Marks">Every non-voided <c>subject_score</c> row for this arm's roster and subjects this term.</param>
public sealed record ArmMarksInput(
    Guid ArmId,
    IReadOnlyList<ComputationPupil> Pupils,
    IReadOnlyList<ComputationSubject> Subjects,
    IReadOnlyList<ComputationMarkRow> Marks);

/// <summary>The whole input to <see cref="ResultComputationEngine.Compute"/>.</summary>
/// <param name="ThisArm">The result set's own arm — every output row is written for these pupils only (human ruling 1).</param>
/// <param name="Components">The assessment structure in force (live or snapshot per spec 8.1).</param>
/// <param name="GradingBands">The grading scale in force.</param>
/// <param name="Rules">The result-rules fields the engine reads.</param>
/// <param name="WriteLevelPosition">
/// Mirrors <c>ResultRules.ShowLevelPosition</c> (spec 6.7.6: "written only when show_level_position is
/// true or primary_position_scope is level" — the caller has already ORed the two).
/// </param>
/// <param name="OtherLevelArms">
/// Every OTHER arm of the same level (human ruling 1: "Rank the whole level from every arm's live,
/// complete, non-voided marks, computed in memory... never read siblings' stored rows, never write or
/// flag sibling sets"). Ignored when <paramref name="WriteLevelPosition"/> is false. Never includes
/// <see cref="ThisArm"/> itself.
/// </param>
public sealed record ComputeResultSetInput(
    ArmMarksInput ThisArm,
    IReadOnlyList<ComputationComponent> Components,
    IReadOnlyList<ComputationGradingBand> GradingBands,
    ComputationRules Rules,
    bool WriteLevelPosition,
    IReadOnlyList<ArmMarksInput>? OtherLevelArms);
