using System.Globalization;
using SchoolManagement.Application.Weekly;

namespace SchoolManagement.Application.Common;

/// <summary>
/// A moment as prints show it (project lead, 2026-09-30): "Sept 30, 2026, 9:58pm WAT", short enough for the result sheet's
/// narrow footer. Never "(Lagos)", which readers took for the school's location.
/// </summary>
public static class PrintedTime
{
    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "June", "July", "Aug", "Sept", "Oct", "Nov", "Dec"];

    /// <summary>An instant, shown in the school's time (WAT, UTC+1 all year).</summary>
    public static string Format(DateTimeOffset instant) => Format(instant.ToOffset(WeeklyProjection.LagosOffset).DateTime);

    /// <summary>A wall-clock time already in the school's time.</summary>
    public static string Format(DateTime schoolTime)
    {
        var hour = schoolTime.Hour % 12 == 0 ? 12 : schoolTime.Hour % 12;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Months[schoolTime.Month - 1]} {schoolTime.Day}, {schoolTime.Year}, {hour}:{schoolTime.Minute:00}{(schoolTime.Hour < 12 ? "am" : "pm")} WAT");
    }
}
