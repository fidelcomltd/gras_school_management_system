namespace SchoolManagement.Domain.Weekly;

/// <summary>One derived week of a term (spec 6.10.3): its number and its Monday and Friday.</summary>
/// <param name="Number">1-based, consecutive from the week containing the term's start date.</param>
/// <param name="StartDate">The Monday.</param>
/// <param name="EndDate">The Friday.</param>
public sealed record TermWeek(int Number, DateOnly StartDate, DateOnly EndDate)
{
    /// <summary>The five weekday dates, Monday first.</summary>
    public IReadOnlyList<DateOnly> Days => [StartDate, StartDate.AddDays(1), StartDate.AddDays(2), StartDate.AddDays(3), StartDate.AddDays(4)];
}

/// <summary>
/// Derives a term's weeks from its dates (spec 6.10.3). Weeks are never typed and never stored as rows of their own:
/// week 1 begins on the Monday of the week containing the start date, and weeks run to the week containing the end date.
/// </summary>
public static class TermWeeks
{
    /// <summary>Spec 6.10.5: <c>week_number</c> is 1 to 20.</summary>
    public const int MaxWeeks = 20;

    /// <summary>The term's weeks, at most <see cref="MaxWeeks"/>. Empty when the end date precedes the start date.</summary>
    public static IReadOnlyList<TermWeek> Derive(DateOnly startDate, DateOnly endDate)
    {
        var monday = MondayOf(startDate);
        var weeks = new List<TermWeek>();
        while (monday <= endDate && weeks.Count < MaxWeeks)
        {
            weeks.Add(new TermWeek(weeks.Count + 1, monday, monday.AddDays(4)));
            monday = monday.AddDays(7);
        }

        return weeks;
    }

    /// <summary>The Monday on or before <paramref name="date"/>. A Saturday or Sunday belongs to the week just ended.</summary>
    public static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
