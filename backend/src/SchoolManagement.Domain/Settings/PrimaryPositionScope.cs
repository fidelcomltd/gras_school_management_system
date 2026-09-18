namespace SchoolManagement.Domain.Settings;

/// <summary>
/// Which position is printed as "Position in Class" (spec 6.2.8). Stored as a string
/// (<c>ResultRulesConfiguration.HasConversion&lt;string&gt;()</c>).
/// </summary>
public enum PrimaryPositionScope
{
    /// <summary>Default. Position within the pupil's own arm.</summary>
    Arm = 0,

    /// <summary>Position across the whole level (every arm at the same class level).</summary>
    Level = 1,
}
