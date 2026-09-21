namespace SchoolManagement.Domain.Results;

/// <summary>
/// Which of the two remark surfaces a <see cref="PupilRemark"/> belongs to (TASK-0086 stage A;
/// spec §6.7.7). Also the discriminator <c>remark_template</c> is keyed on (stage B) — ruling T,
/// 2026-09-19: two lists by kind, not one shared list.
/// </summary>
public enum RemarkKind
{
    /// <summary>The class teacher's remark — <c>result.remark.classteacher</c>, arm-scoped.</summary>
    ClassTeacher = 0,

    /// <summary>The head teacher's remark — <c>result.remark.headteacher</c>, school-wide.</summary>
    HeadTeacher = 1,
}
