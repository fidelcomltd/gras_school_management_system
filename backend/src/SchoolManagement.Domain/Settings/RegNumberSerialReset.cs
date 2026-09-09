namespace SchoolManagement.Domain.Settings;

/// <summary>
/// How the registration-number serial resets (spec 6.2.4, 6.5.10). Stored as a string
/// (<c>SchoolProfileConfiguration.HasConversion&lt;string&gt;()</c>), so a future member is a
/// code-only change.
/// </summary>
/// <remarks>
/// THE REASON THIS CARD EXISTS (approved delta amendment 1): a counter keyed on admission year alone
/// can only express <see cref="PerYear"/>. <see cref="Continuous"/> selects a second, sentinel
/// counter partition (<see cref="RegistrationCounterPartition.ContinuousKey"/>) instead, so the
/// serial genuinely does not restart in January — left keyed on year alone, <c>continuous</c> would
/// be accepted, stored, and silently behave as <see cref="PerYear"/>.
/// </remarks>
public enum RegNumberSerialReset
{
    /// <summary>Default. The serial restarts at 1 for each new admission year.</summary>
    PerYear = 0,

    /// <summary>One running roll number that never restarts, regardless of admission year.</summary>
    Continuous = 1,
}
