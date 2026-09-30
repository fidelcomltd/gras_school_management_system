using SchoolManagement.Application.Common;

namespace SchoolManagement.UnitTests.Application;

/// <summary>Tests for the time printed on every PDF: the school's time, marked WAT, never "Lagos".</summary>
public sealed class PrintedTimeTests
{
    [Theory]
    [InlineData("2026-09-30T20:58:00Z", "Sept 30, 2026, 9:58pm WAT")]
    [InlineData("2026-12-31T23:05:00Z", "Jan 1, 2027, 12:05am WAT")]
    [InlineData("2026-06-15T11:00:00Z", "June 15, 2026, 12:00pm WAT")]
    [InlineData("2026-03-02T07:09:00Z", "Mar 2, 2026, 8:09am WAT")]
    public void Format_ShowsAnInstantInTheSchoolsTime(string instant, string expected) =>
        PrintedTime.Format(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(expected);
}
