using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Weekly;

/// <summary>
/// One pupil's weekly report for one week of a term (spec 6.10.5). Created with its five day rows on the first note, so
/// the grid always has its cells. Nothing here is scored or required; notes are edited, never deleted.
/// </summary>
/// <remarks>
/// <see cref="ArmId"/> is stored, not derived, so a mid-term transfer leaves earlier weeks attributed to the old arm.
/// The week's dates are stored so a printed sheet is reproducible after a term's dates change. Unique on
/// <c>(pupil_id, term_id, week_number)</c>.
/// </remarks>
public sealed class WeeklyReport : Entity<Guid>, IAuditableEntity
{
    private readonly List<WeeklyReportDay> _days = [];

    private WeeklyReport(Guid id, Guid pupilId, Guid armId, Guid termId, TermWeek week, WeeklyReportState state)
        : base(id)
    {
        PupilId = pupilId;
        ArmId = armId;
        TermId = termId;
        WeekNumber = week.Number;
        WeekStartDate = week.StartDate;
        WeekEndDate = week.EndDate;
        State = state;
    }

    // EF Core materialisation constructor.
    private WeeklyReport()
        : base()
    {
    }

    /// <summary>The pupil.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>The arm the pupil sat in that week.</summary>
    public Guid ArmId { get; private set; }

    /// <summary>The term the week falls in.</summary>
    public Guid TermId { get; private set; }

    /// <summary>1 to <see cref="TermWeeks.MaxWeeks"/>, derived, never typed.</summary>
    public int WeekNumber { get; private set; }

    /// <summary>The Monday.</summary>
    public DateOnly WeekStartDate { get; private set; }

    /// <summary>The Friday.</summary>
    public DateOnly WeekEndDate { get; private set; }

    /// <summary>Draft is invisible to parents.</summary>
    public WeeklyReportState State { get; private set; }

    /// <summary>Written on publish.</summary>
    public DateTimeOffset? PublishedAtUtc { get; private set; }

    /// <summary>The publishing account, or null for the automatic Friday publish.</summary>
    public string? PublishedBy { get; private set; }

    /// <summary>Bumped on every note edit; keys the weekly-sheet PDF cache so an edit invalidates it (appendix G.1).</summary>
    public int Revision { get; private set; }

    /// <summary>Monday to Friday.</summary>
    public IReadOnlyList<WeeklyReportDay> Days => _days;

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>True when any day holds any note.</summary>
    public bool HasContent => _days.Exists(day => day.HasContent);

    /// <summary>
    /// Creates the report with its five empty day rows. A report created inside a week that is already published starts
    /// published, because publication is per arm per week (spec 6.10.8).
    /// </summary>
    public static Result<WeeklyReport> Create(Guid id, Guid pupilId, Guid armId, Guid termId, TermWeek week, bool weekIsPublished, DateTimeOffset? publishedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(week);
        if (id == Guid.Empty || pupilId == Guid.Empty || armId == Guid.Empty || termId == Guid.Empty)
        {
            return Result.Failure<WeeklyReport>(Error.Validation("weekly_report.reference_required", "Id, PupilId, ArmId and TermId must not be empty."));
        }

        if (week.Number is < 1 or > TermWeeks.MaxWeeks)
        {
            return Result.Failure<WeeklyReport>(Error.Validation("weekly_report.week_out_of_range", $"Week number must be between 1 and {TermWeeks.MaxWeeks}."));
        }

        var report = new WeeklyReport(id, pupilId, armId, termId, week, weekIsPublished ? WeeklyReportState.Published : WeeklyReportState.Draft)
        {
            PublishedAtUtc = weekIsPublished ? publishedAtUtc : null,
        };

        foreach (var (day, index) in Enum.GetValues<WeeklyDay>().Select((day, index) => (day, index)))
        {
            report._days.Add(WeeklyReportDay.Create(Guid.CreateVersion7(), id, day, week.StartDate.AddDays(index)));
        }

        return Result.Success(report);
    }

    /// <summary>The row for <paramref name="day"/>.</summary>
    public WeeklyReportDay Day(WeeklyDay day) => _days.First(row => row.DayOfWeek == day);

    /// <summary>Writes one cell. Returns false when the value is unchanged, so nothing is stamped or audited.</summary>
    public bool SetNote(WeeklyDay day, WeeklyField field, string? value)
    {
        if (!Day(day).Set(field, value))
        {
            return false;
        }

        Revision++;
        return true;
    }

    /// <summary>Makes the report visible on the portal. Idempotent.</summary>
    public void Publish(DateTimeOffset now, string? actor)
    {
        if (State == WeeklyReportState.Published)
        {
            return;
        }

        State = WeeklyReportState.Published;
        PublishedAtUtc = now;
        PublishedBy = actor;
    }

    /// <summary>Hides the report from the portal. Idempotent; no reason is required (spec 6.10.8).</summary>
    public void Unpublish()
    {
        State = WeeklyReportState.Draft;
        PublishedAtUtc = null;
        PublishedBy = null;
    }
}
