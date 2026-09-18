using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Results;

/// <summary>
/// One pupil's computed mark for one subject in one result set (spec 09 §6.7.6) — TASK-0071.
/// Deleted wholesale and rewritten on every computation (spec 8.2 step 11); never edited in place.
/// </summary>
/// <remarks>
/// Written ONLY for a COMPLETE row (human ruling 2, 2026-09-17): a pupil with no
/// <c>subject_score</c> row, or one missing a required component, has no row here at all for that
/// subject — there is no sentinel for "missing" or "incomplete".
/// </remarks>
public sealed class SubjectResultLine : Entity<Guid>
{
    // EF Core materialisation constructor.
    private SubjectResultLine()
        : base()
    {
        Grade = null!;
        Remark = null!;
    }

    private SubjectResultLine(
        Guid id,
        Guid resultSetId,
        Guid pupilId,
        Guid subjectId,
        int caTotal,
        int? examMark,
        int subjectTotal,
        string grade,
        string remark,
        int? subjectPosition,
        bool subjectPositionTied,
        bool isPass)
        : base(id)
    {
        ResultSetId = resultSetId;
        PupilId = pupilId;
        SubjectId = subjectId;
        CaTotal = caTotal;
        ExamMark = examMark;
        SubjectTotal = subjectTotal;
        Grade = grade;
        Remark = remark;
        SubjectPosition = subjectPosition;
        SubjectPositionTied = subjectPositionTied;
        IsPass = isPass;
    }

    /// <summary>The result set this line was computed for.</summary>
    public Guid ResultSetId { get; private set; }

    /// <summary>The pupil this line belongs to.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>The subject this line belongs to.</summary>
    public Guid SubjectId { get; private set; }

    /// <summary>Sum of the non-examination component marks (spec 8.3).</summary>
    public int CaTotal { get; private set; }

    /// <summary><see langword="null"/> when the pupil was absent for the examination.</summary>
    public int? ExamMark { get; private set; }

    /// <summary><see cref="CaTotal"/> plus <see cref="ExamMark"/>, or <see cref="CaTotal"/> alone when absent.</summary>
    public int SubjectTotal { get; private set; }

    /// <summary>Resolved against the UNROUNDED <see cref="SubjectTotal"/> (spec 6.7.6).</summary>
    public string Grade { get; private set; }

    /// <summary>The matching band's remark.</summary>
    public string Remark { get; private set; }

    /// <summary>Competition rank on <see cref="SubjectTotal"/> descending within the arm (spec 8.2 step 6).</summary>
    public int? SubjectPosition { get; private set; }

    /// <summary>True when this pupil shares <see cref="SubjectPosition"/> with at least one other.</summary>
    public bool SubjectPositionTied { get; private set; }

    /// <summary>True when <see cref="SubjectTotal"/> is at or above <c>pass_mark</c>.</summary>
    public bool IsPass { get; private set; }

    /// <summary>Builds one computed line. Trusts its input — the engine (<c>ResultComputationEngine</c>) has already resolved every value.</summary>
    public static SubjectResultLine Create(
        Guid id,
        Guid resultSetId,
        Guid pupilId,
        Guid subjectId,
        int caTotal,
        int? examMark,
        int subjectTotal,
        string grade,
        string remark,
        int? subjectPosition,
        bool subjectPositionTied,
        bool isPass) =>
        new(id, resultSetId, pupilId, subjectId, caTotal, examMark, subjectTotal, grade, remark, subjectPosition, subjectPositionTied, isPass);
}
