using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Weekly;

/// <summary>
/// One weekday panel of a weekly report (spec 6.10.6): eight free-text lines, none required. A blank value is stored as
/// null, so "empty" has one representation.
/// </summary>
public sealed class WeeklyReportDay : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.10.6: the six observation lines are String 300.</summary>
    public const int LineMaxLength = 300;

    /// <summary>Spec 6.10.6: the two comment lines are String 500.</summary>
    public const int CommentMaxLength = 500;

    private WeeklyReportDay(Guid id, Guid weeklyReportId, WeeklyDay dayOfWeek, DateOnly reportDate)
        : base(id)
    {
        WeeklyReportId = weeklyReportId;
        DayOfWeek = dayOfWeek;
        ReportDate = reportDate;
    }

    // EF Core materialisation constructor.
    private WeeklyReportDay()
        : base()
    {
    }

    /// <summary>The parent report.</summary>
    public Guid WeeklyReportId { get; private set; }

    /// <summary>Monday to Friday.</summary>
    public WeeklyDay DayOfWeek { get; private set; }

    /// <summary>The calendar date, from the week's Monday.</summary>
    public DateOnly ReportDate { get; private set; }

    /// <summary>Behaviour line.</summary>
    public string? Behaviour { get; private set; }

    /// <summary>Performance line.</summary>
    public string? Performance { get; private set; }

    /// <summary>Dressing line.</summary>
    public string? Dressing { get; private set; }

    /// <summary>Home Work line.</summary>
    public string? HomeWork { get; private set; }

    /// <summary>Eating line.</summary>
    public string? Eating { get; private set; }

    /// <summary>Symptoms of illness line.</summary>
    public string? SymptomsOfIllness { get; private set; }

    /// <summary>Teacher's Comment line.</summary>
    public string? TeacherComment { get; private set; }

    /// <summary>Parent's Comment line, transcribed by staff.</summary>
    public string? ParentComment { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>True when any line holds text.</summary>
    public bool HasContent => Enum.GetValues<WeeklyField>().Any(line => Get(line) is not null);

    /// <summary>The maximum length of <paramref name="field"/>.</summary>
    public static int MaxLengthOf(WeeklyField field) =>
        field is WeeklyField.TeacherComment or WeeklyField.ParentComment ? CommentMaxLength : LineMaxLength;

    /// <summary>Trims, and turns blank into null.</summary>
    public static string? Normalise(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Reads one line.</summary>
    public string? Get(WeeklyField field) => field switch
    {
        WeeklyField.Behaviour => Behaviour,
        WeeklyField.Performance => Performance,
        WeeklyField.Dressing => Dressing,
        WeeklyField.HomeWork => HomeWork,
        WeeklyField.Eating => Eating,
        WeeklyField.SymptomsOfIllness => SymptomsOfIllness,
        WeeklyField.TeacherComment => TeacherComment,
        WeeklyField.ParentComment => ParentComment,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    };

    internal static WeeklyReportDay Create(Guid id, Guid weeklyReportId, WeeklyDay day, DateOnly reportDate) =>
        new(id, weeklyReportId, day, reportDate);

    /// <summary>Writes one line; the caller has validated the length. Returns false when unchanged.</summary>
    internal bool Set(WeeklyField field, string? value)
    {
        var normalised = Normalise(value);
        if (string.Equals(Get(field), normalised, StringComparison.Ordinal))
        {
            return false;
        }

        switch (field)
        {
            case WeeklyField.Behaviour: Behaviour = normalised; break;
            case WeeklyField.Performance: Performance = normalised; break;
            case WeeklyField.Dressing: Dressing = normalised; break;
            case WeeklyField.HomeWork: HomeWork = normalised; break;
            case WeeklyField.Eating: Eating = normalised; break;
            case WeeklyField.SymptomsOfIllness: SymptomsOfIllness = normalised; break;
            case WeeklyField.TeacherComment: TeacherComment = normalised; break;
            case WeeklyField.ParentComment: ParentComment = normalised; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, null);
        }

        return true;
    }
}
