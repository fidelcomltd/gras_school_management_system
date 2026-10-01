using System.Globalization;
using SchoolManagement.Application.Common;

namespace SchoolManagement.UnitTests.Application;

/// <summary>Tests for the school's time: WAT, UTC+1, written the way the lead ruled (never "Lagos").</summary>
public sealed class SchoolTimeTests
{
    [Theory]
    [InlineData("2026-09-30T20:58:00Z", "Sept 30, 2026, 9:58pm WAT")]
    [InlineData("2026-12-31T23:05:00Z", "Jan 1, 2027, 12:05am WAT")]
    [InlineData("2026-06-15T11:00:00Z", "June 15, 2026, 12:00pm WAT")]
    [InlineData("2026-03-02T07:09:00+00:00", "Mar 2, 2026, 8:09am WAT")]
    [InlineData("2026-03-02T09:09:00+02:00", "Mar 2, 2026, 8:09am WAT")]
    public void Stamp_ShowsAnInstantOnTheSchoolsClock(string instant, string expected) =>
        SchoolTime.Stamp(DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture)).ShouldBe(expected);

    [Fact]
    public void ClockStamp_IsTheTimeAlone() =>
        SchoolTime.ClockStamp(new DateTimeOffset(2026, 9, 30, 20, 58, 0, TimeSpan.Zero)).ShouldBe("9:58pm WAT");

    [Fact]
    public void Today_TurnsOverAtTheSchoolsMidnight_NotUtcs()
    {
        SchoolTime.Today(new DateTimeOffset(2026, 9, 30, 22, 59, 0, TimeSpan.Zero)).ShouldBe(new DateOnly(2026, 9, 30));
        SchoolTime.Today(new DateTimeOffset(2026, 9, 30, 23, 0, 0, TimeSpan.Zero)).ShouldBe(new DateOnly(2026, 10, 1));
    }
}
