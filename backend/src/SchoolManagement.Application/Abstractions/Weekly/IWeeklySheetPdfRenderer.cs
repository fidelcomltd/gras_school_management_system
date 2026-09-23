namespace SchoolManagement.Application.Abstractions.Weekly;

/// <summary>Everything printed on one weekly report sheet (appendix G). Nothing is computed; every line is typed text.</summary>
/// <param name="SchoolName">Printed above the title.</param>
/// <param name="PupilName">Printed beneath the title, a deliberate addition to the paper form (G.2).</param>
/// <param name="RegistrationNumber">For the file name.</param>
/// <param name="ClassName">Level plus arm label.</param>
/// <param name="WeekNumber">The week.</param>
/// <param name="StartDate">The Monday.</param>
/// <param name="EndDate">The Friday.</param>
/// <param name="TermName">e.g. First Term.</param>
/// <param name="SessionName">e.g. 2026/2027.</param>
/// <param name="Days">Monday to Friday, in the paper form's order.</param>
public sealed record WeeklySheet(
    string SchoolName, string PupilName, string RegistrationNumber, string ClassName, int WeekNumber, DateOnly StartDate, DateOnly EndDate,
    string TermName, string SessionName, IReadOnlyList<WeeklyDaySnapshot> Days)
{
    /// <summary>The report, for the PDF cache key.</summary>
    public Guid ReportId { get; init; }

    /// <summary>The pupil, for the PDF cache key.</summary>
    public Guid PupilId { get; init; }

    /// <summary>The report's revision; any edit bumps it and so invalidates the cached PDF.</summary>
    public int Revision { get; init; }

    /// <summary>The school's current logo upload group, or null.</summary>
    public Guid? LogoGroupId { get; init; }
}

/// <summary>Renders the A4 weekly report sheet (appendix G).</summary>
public interface IWeeklySheetPdfRenderer
{
    /// <summary>One sheet. <paramref name="logo"/> may be empty.</summary>
    byte[] Render(WeeklySheet sheet, ReadOnlyMemory<byte> logo, DateTimeOffset printedAt);
}
