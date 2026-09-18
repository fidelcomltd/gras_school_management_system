namespace SchoolManagement.Domain.Pupils;

/// <summary>
/// A <see cref="Pupil"/>'s status (spec 6.5.4, 6.5.14). Defaults <see cref="Pending"/>.
/// </summary>
/// <remarks>
/// TASK-0050 creates records ONLY in <see cref="Pending"/> — every other member exists so the
/// column's shape is right and so <c>status=</c> filters over 6.5.14's later transitions do not need
/// a migration, but nothing in this card ever writes them. No status-change endpoint exists yet; see
/// <c>backend/docs/ASSUMPTIONS.md</c> §2.27.
/// </remarks>
public enum PupilStatus
{
    /// <summary>
    /// In the admissions queue, not yet approved. The default. Spec 6.5.14: "excluded from every arm
    /// roster, every enrolment count, every capacity calculation, every score sheet, every result
    /// set, every weekly report, every report and every portal lookup. It exists in the admissions
    /// queue and nowhere else."
    /// </summary>
    Pending,

    /// <summary>Approved and enrolled. Set only by admission approval (6.5.14) — not built this card.</summary>
    Active,

    /// <summary>Moved to another school arm mid-session (6.5.14) — not built this card.</summary>
    Transferred,

    /// <summary>Left the school (6.5.14) — not built this card.</summary>
    Withdrawn,

    /// <summary>Completed the graduating level (6.5.14) — not built this card.</summary>
    Graduated,
}
