using System.Globalization;
using SchoolManagement.Application.Abstractions.Weekly;
using SchoolManagement.Domain.Weekly;

namespace SchoolManagement.Application.Weekly;

/// <summary>Shared shaping for the weekly-report reads, so the grid, the pupil view and the portal agree.</summary>
internal static class WeeklyProjection
{
    /// <summary>Lagos is UTC+1 all year (no daylight saving), so "today" and "17:00 on Friday" are a fixed offset.</summary>
    public static readonly TimeSpan LagosOffset = TimeSpan.FromHours(1);

    /// <summary>The school's calendar date at <paramref name="now"/>.</summary>
    public static DateOnly LagosToday(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(LagosOffset).DateTime);

    public static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    public static string DisplayName(string surname, string firstName, string? middleName) =>
        string.IsNullOrWhiteSpace(middleName) ? $"{surname} {firstName}" : $"{surname} {firstName} {middleName}";

    /// <summary>Five days for a report, or five blank days dated from <paramref name="weekStart"/> when there is none.</summary>
    public static IReadOnlyList<WeeklyDayDto> Days(WeeklyReportSnapshot? report, DateOnly weekStart) =>
        report is null
            ? Enum.GetValues<WeeklyDay>().Select((day, index) => Blank(day, weekStart.AddDays(index))).ToArray()
            : report.Days.Select(Day).ToArray();

    public static WeeklyDayDto Day(WeeklyDaySnapshot day)
    {
        ArgumentNullException.ThrowIfNull(day);
        return new WeeklyDayDto(
            day.Day,
            day.Date,
            day.Get(WeeklyField.Behaviour),
            day.Get(WeeklyField.Performance),
            day.Get(WeeklyField.Dressing),
            day.Get(WeeklyField.HomeWork),
            day.Get(WeeklyField.Eating),
            day.Get(WeeklyField.SymptomsOfIllness),
            day.Get(WeeklyField.TeacherComment),
            day.Get(WeeklyField.ParentComment),
            day.EditedAt,
            day.EditedById,
            day.EditedByName);
    }

    /// <summary>
    /// The term's weeks as an arm (or a pupil) sees them: every derived week, plus any stored week that no longer matches
    /// a derived one, flagged as outside the term rather than hidden (spec 6.10.10: nothing is deleted).
    /// </summary>
    public static IReadOnlyList<WeeklyWeekSummaryDto> Weeks(IReadOnlyList<TermWeek> derived, IReadOnlyList<WeeklyArmWeekTotals> totals)
    {
        ArgumentNullException.ThrowIfNull(derived);
        ArgumentNullException.ThrowIfNull(totals);

        var byWeek = totals.GroupBy(total => total.WeekNumber).ToDictionary(group => group.Key, group => group.ToList());
        var weeks = new List<WeeklyWeekSummaryDto>();
        foreach (var week in derived)
        {
            var rows = byWeek.GetValueOrDefault(week.Number) ?? [];
            weeks.Add(new WeeklyWeekSummaryDto(
                week.Number,
                week.StartDate,
                week.EndDate,
                rows.Exists(row => row.StartDate != week.StartDate),
                rows.Exists(row => row.Published),
                rows.Sum(row => row.PupilsWithNotes)));
        }

        foreach (var (number, rows) in byWeek.Where(pair => pair.Key > derived.Count).OrderBy(pair => pair.Key))
        {
            weeks.Add(new WeeklyWeekSummaryDto(
                number, rows.Min(row => row.StartDate), rows.Min(row => row.EndDate), OutsideTerm: true,
                rows.Exists(row => row.Published), rows.Sum(row => row.PupilsWithNotes)));
        }

        return weeks;
    }

    /// <summary>The derived week containing <paramref name="today"/>, else the nearest end of the term.</summary>
    public static int CurrentWeek(IReadOnlyList<TermWeek> derived, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(derived);
        if (derived.Count == 0)
        {
            return 1;
        }

        var monday = TermWeeks.MondayOf(today);
        return derived.FirstOrDefault(week => week.StartDate == monday)?.Number
            ?? (today < derived[0].StartDate ? 1 : derived[^1].Number);
    }

    public static WeeklyPhrasesDto Phrases(IReadOnlyDictionary<WeeklyField, IReadOnlyList<string>> phrases)
    {
        ArgumentNullException.ThrowIfNull(phrases);
        IReadOnlyList<string> Of(WeeklyField field) => phrases.GetValueOrDefault(field) ?? [];
        return new WeeklyPhrasesDto(
            Of(WeeklyField.Behaviour), Of(WeeklyField.Performance), Of(WeeklyField.Dressing), Of(WeeklyField.HomeWork),
            Of(WeeklyField.Eating), Of(WeeklyField.SymptomsOfIllness), Of(WeeklyField.TeacherComment), Of(WeeklyField.ParentComment));
    }

    private static WeeklyDayDto Blank(WeeklyDay day, DateOnly date) =>
        new(day, date, null, null, null, null, null, null, null, null, null, null, null);
}
