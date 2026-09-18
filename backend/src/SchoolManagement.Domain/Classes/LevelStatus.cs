namespace SchoolManagement.Domain.Classes;

/// <summary>A <see cref="ClassLevel"/>'s status (spec 6.4.2). Defaults <see cref="Active"/>.</summary>
public enum LevelStatus
{
    /// <summary>Part of the active progression chain; selectable for new arms and new enrolment.</summary>
    Active,

    /// <summary>
    /// Excluded from the active chain, new arm creation, new enrolment and the promotion target
    /// selector (spec 6.4.2). Every existing arm, enrolment, mark and published result remains
    /// exactly as it was.
    /// </summary>
    Inactive,
}
