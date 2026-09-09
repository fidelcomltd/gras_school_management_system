namespace SchoolManagement.Domain.Classes;

/// <summary>
/// An <see cref="Arm"/>'s status (spec 6.4.3). Defaults <see cref="Active"/>.
/// </summary>
public enum ArmStatus
{
    /// <summary>Open for enrolment and every ordinary mutation.</summary>
    Active,

    /// <summary>
    /// Hidden from new enrolment, but its roster and results stay readable. An administrator sets
    /// this directly (spec 6.4.7) — unlike <see cref="Closed"/>, it is reversible.
    /// </summary>
    Inactive,

    /// <summary>
    /// Set automatically when the arm's session closes (spec 6.4.7), never directly by a caller. Every
    /// mutation is rejected from this state on — see <see cref="Arm.EnsureMutable"/>.
    /// </summary>
    Closed,
}
