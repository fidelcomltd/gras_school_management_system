using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Results;

/// <summary>
/// One subject's class statistics for one arm's result set (spec 09 §6.7.6) — TASK-0071. One row per
/// subject in effect, always written, even when every counted population is empty (spec 6.7.12: "No
/// examination sat"). Deleted wholesale and rewritten on every computation.
/// </summary>
public sealed class SubjectArmStatistic : Entity<Guid>
{
    // EF Core materialisation constructor.
    private SubjectArmStatistic()
        : base()
    {
    }

    private SubjectArmStatistic(
        Guid id,
        Guid resultSetId,
        Guid subjectId,
        int? highestScore,
        int? lowestScore,
        decimal? classAverage,
        int countedPupils,
        int rankedPupils)
        : base(id)
    {
        ResultSetId = resultSetId;
        SubjectId = subjectId;
        HighestScore = highestScore;
        LowestScore = lowestScore;
        ClassAverage = classAverage;
        CountedPupils = countedPupils;
        RankedPupils = rankedPupils;
    }

    /// <summary>The result set this statistic was computed for.</summary>
    public Guid ResultSetId { get; private set; }

    /// <summary>The subject this statistic is for.</summary>
    public Guid SubjectId { get; private set; }

    /// <summary>Maximum subject_total among counted pupils. <see langword="null"/> when <see cref="CountedPupils"/> is 0.</summary>
    public int? HighestScore { get; private set; }

    /// <summary>Minimum subject_total among counted pupils. <see langword="null"/> when <see cref="CountedPupils"/> is 0.</summary>
    public int? LowestScore { get; private set; }

    /// <summary>Rounded half up to one decimal place. <see langword="null"/> when <see cref="CountedPupils"/> is 0.</summary>
    public decimal? ClassAverage { get; private set; }

    /// <summary>Pupils with a complete, non-exam-absent score (spec 6.7.6's highest/lowest/average population).</summary>
    public int CountedPupils { get; private set; }

    /// <summary>Every pupil with a complete score, including exam absentees (spec 8.2 step 6's ranked population).</summary>
    public int RankedPupils { get; private set; }

    /// <summary>Builds one computed statistic row. Trusts its input — the engine has already resolved every value.</summary>
    public static SubjectArmStatistic Create(
        Guid id,
        Guid resultSetId,
        Guid subjectId,
        int? highestScore,
        int? lowestScore,
        decimal? classAverage,
        int countedPupils,
        int rankedPupils) =>
        new(id, resultSetId, subjectId, highestScore, lowestScore, classAverage, countedPupils, rankedPupils);
}
