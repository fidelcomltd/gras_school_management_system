using SchoolManagement.Domain.Weekly;

namespace SchoolManagement.Application.Abstractions.Weekly;

/// <summary>One day panel, read-only, with its last editor resolved to a staff name.</summary>
/// <param name="Day">Monday to Friday.</param>
/// <param name="Date">The calendar date.</param>
/// <param name="Notes">The eight lines, indexed by <see cref="WeeklyField"/>.</param>
/// <param name="EditedAt">When the panel was last written.</param>
/// <param name="EditedById">The account that last wrote it.</param>
/// <param name="EditedByName">Its staff name.</param>
public sealed record WeeklyDaySnapshot(
    WeeklyDay Day, DateOnly Date, IReadOnlyList<string?> Notes, DateTimeOffset? EditedAt, string? EditedById, string? EditedByName)
{
    /// <summary>One line.</summary>
    public string? Get(WeeklyField field) => Notes[(int)field];

    /// <summary>True when any line holds text.</summary>
    public bool HasContent => Notes.Any(note => note is not null);
}

/// <summary>One weekly report, read-only, with its five days in order.</summary>
/// <param name="Id">The report.</param>
/// <param name="PupilId">The pupil.</param>
/// <param name="ArmId">The arm the pupil sat in that week.</param>
/// <param name="WeekNumber">1 to 20.</param>
/// <param name="StartDate">The stored Monday.</param>
/// <param name="EndDate">The stored Friday.</param>
/// <param name="State">Draft or Published.</param>
/// <param name="PublishedAt">When published.</param>
/// <param name="Revision">Bumped on every edit.</param>
/// <param name="Days">Monday to Friday.</param>
public sealed record WeeklyReportSnapshot(
    Guid Id, Guid PupilId, Guid ArmId, int WeekNumber, DateOnly StartDate, DateOnly EndDate, WeeklyReportState State,
    DateTimeOffset? PublishedAt, int Revision, IReadOnlyList<WeeklyDaySnapshot> Days)
{
    /// <summary>True when any day holds any note.</summary>
    public bool HasContent => Days.Any(day => day.HasContent);
}

/// <summary>One arm's week for the completion report (spec 6.10.12).</summary>
/// <param name="ArmId">The arm.</param>
/// <param name="WeekNumber">The week.</param>
/// <param name="StartDate">The stored Monday (earliest, should two ever differ).</param>
/// <param name="EndDate">The stored Friday.</param>
/// <param name="PupilsWithNotes">Pupils with at least one note.</param>
/// <param name="CellsFilled">Non-empty lines across every pupil and day.</param>
/// <param name="Published">Whether the week is published.</param>
/// <param name="LastEditedAt">The latest edit.</param>
/// <param name="LastEditedById">Who made it.</param>
public sealed record WeeklyArmWeekTotals(
    Guid ArmId, int WeekNumber, DateOnly StartDate, DateOnly EndDate, int PupilsWithNotes, int CellsFilled, bool Published, DateTimeOffset? LastEditedAt, string? LastEditedById);

/// <summary>One recorded symptoms line, for the illness observation summary (spec 6.10.12).</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="ArmId">The arm the week is attributed to.</param>
/// <param name="Date">The day.</param>
/// <param name="Text">What was written.</param>
public sealed record WeeklySymptomsEntry(Guid PupilId, Guid ArmId, DateOnly Date, string Text);

/// <summary>Persistence port for weekly reports (spec 6.10).</summary>
public interface IWeeklyReportRepository
{
    /// <summary>Every report for one arm's week, with days. <c>AsNoTracking</c>.</summary>
    Task<IReadOnlyList<WeeklyReportSnapshot>> ListArmWeekAsync(Guid armId, Guid termId, int weekNumber, CancellationToken cancellationToken);

    /// <summary>Every report for one pupil's term, with days, in week order. <c>AsNoTracking</c>.</summary>
    Task<IReadOnlyList<WeeklyReportSnapshot>> ListPupilTermAsync(Guid pupilId, Guid termId, CancellationToken cancellationToken);

    /// <summary>Per week of one arm's term, whether any row is published and how many pupils have notes.</summary>
    Task<IReadOnlyList<WeeklyArmWeekTotals>> SummariseAsync(Guid termId, Guid? armId, CancellationToken cancellationToken);

    /// <summary>
    /// The distinct values <paramref name="actorId"/> has written this term, per field, most recent first (spec 6.10.7
    /// phrase memory).
    /// </summary>
    Task<IReadOnlyDictionary<WeeklyField, IReadOnlyList<string>>> ListPhrasesAsync(
        Guid termId, string actorId, int perField, CancellationToken cancellationToken);

    /// <summary>Every non-empty symptoms line in the term.</summary>
    Task<IReadOnlyList<WeeklySymptomsEntry>> ListSymptomsAsync(Guid termId, CancellationToken cancellationToken);

    /// <summary>The reports for <paramref name="pupilIds"/> in one week of a term, TRACKED, with days.</summary>
    Task<IReadOnlyList<WeeklyReport>> ListTrackedAsync(
        Guid termId, int weekNumber, IReadOnlyCollection<Guid> pupilIds, CancellationToken cancellationToken);

    /// <summary>Every report for one arm's week, TRACKED, with days.</summary>
    Task<IReadOnlyList<WeeklyReport>> ListArmWeekTrackedAsync(Guid armId, Guid termId, int weekNumber, CancellationToken cancellationToken);

    /// <summary>Stages a new report with its days. Does NOT commit.</summary>
    Task AddAsync(WeeklyReport report, CancellationToken cancellationToken);

    /// <summary>The arm's setting row, TRACKED, or null (auto-publish off).</summary>
    Task<ArmWeeklySetting?> FindSettingTrackedAsync(Guid armId, CancellationToken cancellationToken);

    /// <summary>Whether the arm auto-publishes.</summary>
    Task<bool> IsAutoPublishAsync(Guid armId, CancellationToken cancellationToken);

    /// <summary>Stages a new setting row. Does NOT commit.</summary>
    Task AddSettingAsync(ArmWeeklySetting setting, CancellationToken cancellationToken);
}
