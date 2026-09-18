using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Results;

/// <summary>
/// One pupil's computed overall result for one result set (spec 09 §6.7.6) — TASK-0071. One row per
/// pupil on the arm's roster at computation time, always written. Deleted wholesale and rewritten on
/// every computation.
/// </summary>
public sealed class PupilTermResult : Entity<Guid>
{
    // EF Core materialisation constructor.
    private PupilTermResult()
        : base()
    {
        OverallGrade = null!;
    }

    private PupilTermResult(
        Guid id,
        Guid resultSetId,
        Guid pupilId,
        int subjectsTaken,
        int totalObtainable,
        int totalObtained,
        decimal average,
        string overallGrade,
        int? armPosition,
        bool armPositionTied,
        int armPupilCount,
        int? levelPosition,
        bool levelPositionTied,
        int? levelPupilCount)
        : base(id)
    {
        ResultSetId = resultSetId;
        PupilId = pupilId;
        SubjectsTaken = subjectsTaken;
        TotalObtainable = totalObtainable;
        TotalObtained = totalObtained;
        Average = average;
        OverallGrade = overallGrade;
        ArmPosition = armPosition;
        ArmPositionTied = armPositionTied;
        ArmPupilCount = armPupilCount;
        LevelPosition = levelPosition;
        LevelPositionTied = levelPositionTied;
        LevelPupilCount = levelPupilCount;
    }

    /// <summary>The result set this row was computed for.</summary>
    public Guid ResultSetId { get; private set; }

    /// <summary>The pupil this row belongs to.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>Count of subjects in effect for the arm this term (literal — human ruling 2).</summary>
    public int SubjectsTaken { get; private set; }

    /// <summary><see cref="SubjectsTaken"/> multiplied by 100.</summary>
    public int TotalObtainable { get; private set; }

    /// <summary>Sum of the pupil's complete subject totals; a missing/incomplete subject adds 0 (human ruling 2).</summary>
    public int TotalObtained { get; private set; }

    /// <summary><see cref="TotalObtained"/> divided by <see cref="SubjectsTaken"/>, rounded half up to two decimal places.</summary>
    public decimal Average { get; private set; }

    /// <summary>Resolved against the ROUNDED <see cref="Average"/> (spec 6.7.6).</summary>
    public string OverallGrade { get; private set; }

    /// <summary>Competition rank on <see cref="TotalObtained"/> descending within the arm. <see langword="null"/> below <c>min_subjects_for_position</c>.</summary>
    public int? ArmPosition { get; private set; }

    /// <summary>True when this pupil shares <see cref="ArmPosition"/> with at least one other.</summary>
    public bool ArmPositionTied { get; private set; }

    /// <summary>Number of pupils ranked (eligible for position) in the arm.</summary>
    public int ArmPupilCount { get; private set; }

    /// <summary>Competition rank on <see cref="Average"/> descending across every arm of the level (human ruling 1). <see langword="null"/> unless requested.</summary>
    public int? LevelPosition { get; private set; }

    /// <summary>True when this pupil shares <see cref="LevelPosition"/> with at least one other.</summary>
    public bool LevelPositionTied { get; private set; }

    /// <summary>Number of pupils ranked across the whole level. <see langword="null"/> unless level position was requested.</summary>
    public int? LevelPupilCount { get; private set; }

    /// <summary>Builds one computed pupil result row. Trusts its input — the engine has already resolved every value.</summary>
    public static PupilTermResult Create(
        Guid id,
        Guid resultSetId,
        Guid pupilId,
        int subjectsTaken,
        int totalObtainable,
        int totalObtained,
        decimal average,
        string overallGrade,
        int? armPosition,
        bool armPositionTied,
        int armPupilCount,
        int? levelPosition,
        bool levelPositionTied,
        int? levelPupilCount) =>
        new(
            id, resultSetId, pupilId, subjectsTaken, totalObtainable, totalObtained, average, overallGrade,
            armPosition, armPositionTied, armPupilCount, levelPosition, levelPositionTied, levelPupilCount);
}
