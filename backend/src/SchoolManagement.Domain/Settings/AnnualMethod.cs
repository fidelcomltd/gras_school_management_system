namespace SchoolManagement.Domain.Settings;

/// <summary>
/// How the annual average is computed from the three terminal averages (spec 6.2.8). Stored as a
/// string (<c>ResultRulesConfiguration.HasConversion&lt;string&gt;()</c>), so a future member is a
/// code-only change, matching <see cref="RegNumberSerialReset"/>'s convention.
/// </summary>
public enum AnnualMethod
{
    /// <summary>Default. Simple average of the three terminal averages (spec 6.2.8: "what parents expect").</summary>
    SimpleAverage = 0,

    /// <summary>
    /// Weighted by <see cref="ResultRules.WeightFirst"/>/<see cref="ResultRules.WeightSecond"/>/
    /// <see cref="ResultRules.WeightThird"/>. Opt-in (spec 6.2.8: "I have made it opt-in").
    /// </summary>
    Weighted = 1,
}
