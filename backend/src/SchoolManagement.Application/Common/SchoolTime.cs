using System.Globalization;

namespace SchoolManagement.Application.Common;

/// <summary>
/// The school's time: West African Time, UTC+1 all year (Nigeria keeps no daylight saving). The one home of that offset,
/// and of how a time is written for people to read (<see cref="Stamp"/>, <see cref="ClockStamp"/>).
/// </summary>
public static class SchoolTime
{
    /// <summary>WAT's offset from UTC.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(1);

    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "June", "July", "Aug", "Sept", "Oct", "Nov", "Dec"];

    /// <summary>An instant as the school's wall clock shows it.</summary>
    public static DateTimeOffset At(DateTimeOffset instant) => instant.ToOffset(Offset);

    /// <summary>The school's calendar date at an instant.</summary>
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(At(now).DateTime);

    /// <summary>A date and time for people to read: "Sept 30, 2026, 9:58pm WAT".</summary>
    public static string Stamp(DateTimeOffset instant)
    {
        var at = At(instant);
        return string.Create(CultureInfo.InvariantCulture, $"{Months[at.Month - 1]} {at.Day}, {at.Year}, {ClockStamp(instant)}");
    }

    /// <summary>A time alone, for a page that already gives the date: "9:58pm WAT".</summary>
    public static string ClockStamp(DateTimeOffset instant)
    {
        var at = At(instant);
        var hour = at.Hour % 12 == 0 ? 12 : at.Hour % 12;
        return string.Create(CultureInfo.InvariantCulture, $"{hour}:{at.Minute:00}{(at.Hour < 12 ? "am" : "pm")} WAT");
    }
}
