namespace SchoolManagement.Domain.Settings;

/// <summary>
/// How a tied subject/annual total is broken for position ranking (spec 6.2.8). Stored as a string
/// (<c>ResultRulesConfiguration.HasConversion&lt;string&gt;()</c>).
/// </summary>
public enum TieBreakRule
{
    /// <summary>Default. Tied pupils share the same printed position.</summary>
    SharedPosition = 0,

    /// <summary>Ties broken by the examination mark, then the continuous-assessment total.</summary>
    ExamThenCa = 1,

    /// <summary>Ties broken by the examination mark, then alphabetically by pupil name.</summary>
    ExamThenAlphabetical = 2,
}
