namespace SchoolManagement.Application.Results.Computation;

/// <summary>One row of <c>subject_result_line</c> (spec 6.7.6) — written only for a COMPLETE row (human ruling 2).</summary>
/// <param name="PupilId">The pupil this line belongs to.</param>
/// <param name="SubjectId">The subject this line belongs to.</param>
/// <param name="CaTotal">Sum of the non-examination component marks (spec 8.3).</param>
/// <param name="ExamMark">Null when the pupil was absent for the examination.</param>
/// <param name="SubjectTotal"><see cref="CaTotal"/> plus <see cref="ExamMark"/>, or <see cref="CaTotal"/> alone when absent.</param>
/// <param name="Grade">Resolved against the unrounded <see cref="SubjectTotal"/> (spec 6.7.6).</param>
/// <param name="Remark">The matching band's remark.</param>
/// <param name="SubjectPosition">Null only when <see cref="SubjectTotal"/> matches no band would have failed the whole computation first — always non-null here.</param>
/// <param name="SubjectPositionTied">True when this pupil shares <see cref="SubjectPosition"/> with at least one other (spec 6.7.6's tie display).</param>
/// <param name="IsPass">True when <see cref="SubjectTotal"/> is at or above <c>pass_mark</c>.</param>
public sealed record SubjectResultLineOutput(
    Guid PupilId,
    Guid SubjectId,
    int CaTotal,
    int? ExamMark,
    int SubjectTotal,
    string Grade,
    string Remark,
    int? SubjectPosition,
    bool SubjectPositionTied,
    bool IsPass);

/// <summary>One row of <c>subject_arm_statistic</c> (spec 6.7.6) — one per subject in effect, always written.</summary>
/// <param name="SubjectId">The subject this statistic is for.</param>
/// <param name="HighestScore">Null when <see cref="CountedPupils"/> is 0 (spec 6.7.12: "No examination sat").</param>
/// <param name="LowestScore">Null when <see cref="CountedPupils"/> is 0.</param>
/// <param name="ClassAverage">Rounded half up to one decimal place. Null when <see cref="CountedPupils"/> is 0.</param>
/// <param name="CountedPupils">Complete, non-exam-absent rows (spec 6.7.6: highest/lowest/average population).</param>
/// <param name="RankedPupils">Every complete row, including exam absentees (spec 6.7.6's subject_position population).</param>
public sealed record SubjectArmStatisticOutput(
    Guid SubjectId,
    int? HighestScore,
    int? LowestScore,
    decimal? ClassAverage,
    int CountedPupils,
    int RankedPupils);

/// <summary>One row of <c>pupil_term_result</c> (spec 6.7.6) — written for every pupil on <c>ThisArm</c>'s roster.</summary>
/// <param name="PupilId">The pupil this result belongs to.</param>
/// <param name="SubjectsTaken">Count of subjects in effect for the arm this term (literal, not "subjects scored" — human ruling 2).</param>
/// <param name="TotalObtainable"><see cref="SubjectsTaken"/> multiplied by 100.</param>
/// <param name="TotalObtained">Sum of <see cref="SubjectResultLineOutput.SubjectTotal"/> across the pupil's COMPLETE subjects; a missing/incomplete subject adds 0 (human ruling 2).</param>
/// <param name="Average"><see cref="TotalObtained"/> divided by <see cref="SubjectsTaken"/>, rounded half up to two decimal places.</param>
/// <param name="OverallGrade">Resolved against the ROUNDED <see cref="Average"/> (spec 6.7.6).</param>
/// <param name="ArmPosition">Null when the pupil's scored-subject count is below <c>min_subjects_for_position</c>.</param>
/// <param name="ArmPositionTied">True when this pupil shares <see cref="ArmPosition"/> with at least one other.</param>
/// <param name="ArmPupilCount">Number of pupils ranked (eligible for position) in the arm.</param>
/// <param name="LevelPosition">Null unless level position was requested. Computed across every arm of the level on <see cref="Average"/> descending (human ruling 1).</param>
/// <param name="LevelPositionTied">True when this pupil shares <see cref="LevelPosition"/> with at least one other.</param>
/// <param name="LevelPupilCount">Number of pupils ranked across the whole level. Null unless level position was requested.</param>
public sealed record PupilTermResultOutput(
    Guid PupilId,
    int SubjectsTaken,
    int TotalObtainable,
    int TotalObtained,
    decimal Average,
    string OverallGrade,
    int? ArmPosition,
    bool ArmPositionTied,
    int ArmPupilCount,
    int? LevelPosition,
    bool LevelPositionTied,
    int? LevelPupilCount);

/// <summary>Stable <c>flags[].code</c> values (contract delta — NOT the dotted <c>result_set.*</c> problem-code convention; these stay as written).</summary>
public static class ComputationFlagCodes
{
    /// <summary>Spec 6.7.12: every pupil counted for a subject was absent for its examination.</summary>
    public const string NoExaminationSat = "no_examination_sat";

    /// <summary>Spec 6.7.4: a pupil was absent for the examination in every one of their complete subjects.</summary>
    public const string AbsentAllExaminations = "absent_all_examinations";
}

/// <summary>One computation flag (contract delta's <c>flags[]</c>).</summary>
/// <param name="Code">One of <see cref="ComputationFlagCodes"/>.</param>
/// <param name="SubjectId">Set for <see cref="ComputationFlagCodes.NoExaminationSat"/>.</param>
/// <param name="PupilId">Set for <see cref="ComputationFlagCodes.AbsentAllExaminations"/>.</param>
public sealed record ComputationFlag(string Code, Guid? SubjectId, Guid? PupilId);

/// <summary>The whole output of <see cref="ResultComputationEngine.Compute"/> — the three computed tables' new rows for <c>ThisArm</c>, plus flags.</summary>
public sealed record ComputeResultSetOutput(
    IReadOnlyList<SubjectResultLineOutput> SubjectLines,
    IReadOnlyList<SubjectArmStatisticOutput> SubjectStatistics,
    IReadOnlyList<PupilTermResultOutput> PupilResults,
    IReadOnlyList<ComputationFlag> Flags);
