using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Weekly;
using SchoolManagement.Application.Weekly;
using SchoolManagement.Domain.Weekly;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IWeeklyReportRepository"/> and <see cref="IWeeklyNameLookup"/> (spec 6.10).</summary>
internal sealed class WeeklyReportRepository(ApplicationDbContext context) : IWeeklyReportRepository, IWeeklyNameLookup
{
    /// <inheritdoc />
    public Task<IReadOnlyList<WeeklyReportSnapshot>> ListArmWeekAsync(Guid armId, Guid termId, int weekNumber, CancellationToken cancellationToken) =>
        SnapshotsAsync(context.WeeklyReports.Where(report => report.ArmId == armId && report.TermId == termId && report.WeekNumber == weekNumber), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WeeklyReportSnapshot>> ListPupilTermAsync(Guid pupilId, Guid termId, CancellationToken cancellationToken) =>
        SnapshotsAsync(context.WeeklyReports.Where(report => report.PupilId == pupilId && report.TermId == termId), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<WeeklyArmWeekTotals>> SummariseAsync(Guid termId, Guid? armId, CancellationToken cancellationToken)
    {
        var reports = context.WeeklyReports.AsNoTracking().Where(report => report.TermId == termId && (armId == null || report.ArmId == armId));
        var rows = await (
                from report in reports
                select new
                {
                    report.ArmId,
                    report.WeekNumber,
                    report.WeekStartDate,
                    report.WeekEndDate,
                    Published = report.State == WeeklyReportState.Published,
                    Filled = context.WeeklyReportDays.Where(day => day.WeeklyReportId == report.Id).Sum(day =>
                        (day.Behaviour != null ? 1 : 0) + (day.Performance != null ? 1 : 0) + (day.Dressing != null ? 1 : 0) +
                        (day.HomeWork != null ? 1 : 0) + (day.Eating != null ? 1 : 0) + (day.SymptomsOfIllness != null ? 1 : 0) +
                        (day.TeacherComment != null ? 1 : 0) + (day.ParentComment != null ? 1 : 0)),
                    EditedAt = report.ModifiedAtUtc ?? report.CreatedAtUtc,
                    EditedBy = report.ModifiedBy ?? report.CreatedBy,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(row => (row.ArmId, row.WeekNumber))
            .Select(group =>
            {
                var latest = group.MaxBy(row => row.EditedAt)!;
                return new WeeklyArmWeekTotals(
                    group.Key.ArmId, group.Key.WeekNumber, group.Min(row => row.WeekStartDate), group.Min(row => row.WeekEndDate),
                    group.Count(row => row.Filled > 0), group.Sum(row => row.Filled), group.Any(row => row.Published),
                    group.Any(row => row.Filled > 0) ? latest.EditedAt : null, group.Any(row => row.Filled > 0) ? latest.EditedBy : null);
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<WeeklyField, IReadOnlyList<string>>> ListPhrasesAsync(
        Guid termId, string actorId, int perField, CancellationToken cancellationToken)
    {
        // A day row last written by this account: its lines are what this account typed, or kept, most recently.
        var days = await (
                from day in context.WeeklyReportDays.AsNoTracking()
                join report in context.WeeklyReports.AsNoTracking() on day.WeeklyReportId equals report.Id
                where report.TermId == termId && (day.ModifiedBy ?? day.CreatedBy) == actorId
                orderby (day.ModifiedAtUtc ?? day.CreatedAtUtc) descending
                select new
                {
                    day.Behaviour,
                    day.Performance,
                    day.Dressing,
                    day.HomeWork,
                    day.Eating,
                    day.SymptomsOfIllness,
                    day.TeacherComment,
                    day.ParentComment,
                })
            .Take(500)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<string> Distinct(IEnumerable<string?> values) =>
            values.OfType<string>().Distinct(StringComparer.Ordinal).Take(perField).ToList();

        return new Dictionary<WeeklyField, IReadOnlyList<string>>
        {
            [WeeklyField.Behaviour] = Distinct(days.Select(day => day.Behaviour)),
            [WeeklyField.Performance] = Distinct(days.Select(day => day.Performance)),
            [WeeklyField.Dressing] = Distinct(days.Select(day => day.Dressing)),
            [WeeklyField.HomeWork] = Distinct(days.Select(day => day.HomeWork)),
            [WeeklyField.Eating] = Distinct(days.Select(day => day.Eating)),
            [WeeklyField.SymptomsOfIllness] = Distinct(days.Select(day => day.SymptomsOfIllness)),
            [WeeklyField.TeacherComment] = Distinct(days.Select(day => day.TeacherComment)),
            [WeeklyField.ParentComment] = Distinct(days.Select(day => day.ParentComment)),
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WeeklySymptomsEntry>> ListSymptomsAsync(Guid termId, CancellationToken cancellationToken) =>
        await (
                from day in context.WeeklyReportDays.AsNoTracking()
                join report in context.WeeklyReports.AsNoTracking() on day.WeeklyReportId equals report.Id
                where report.TermId == termId && day.SymptomsOfIllness != null
                select new WeeklySymptomsEntry(report.PupilId, report.ArmId, day.ReportDate, day.SymptomsOfIllness!))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<WeeklyReport>> ListTrackedAsync(
        Guid termId, int weekNumber, IReadOnlyCollection<Guid> pupilIds, CancellationToken cancellationToken) =>
        await context.WeeklyReports
            .Include(report => report.Days)
            .Where(report => report.TermId == termId && report.WeekNumber == weekNumber && pupilIds.Contains(report.PupilId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<WeeklyReport>> ListArmWeekTrackedAsync(Guid armId, Guid termId, int weekNumber, CancellationToken cancellationToken) =>
        await context.WeeklyReports
            .Include(report => report.Days)
            .Where(report => report.ArmId == armId && report.TermId == termId && report.WeekNumber == weekNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task AddAsync(WeeklyReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();
        context.WeeklyReports.Add(report);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ArmWeeklySetting?> FindSettingTrackedAsync(Guid armId, CancellationToken cancellationToken) =>
        context.ArmWeeklySettings.FirstOrDefaultAsync(setting => setting.ArmId == armId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> IsAutoPublishAsync(Guid armId, CancellationToken cancellationToken) =>
        context.ArmWeeklySettings.AsNoTracking().AnyAsync(setting => setting.ArmId == armId && setting.AutoPublish, cancellationToken);

    /// <inheritdoc />
    public Task AddSettingAsync(ArmWeeklySetting setting, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setting);
        cancellationToken.ThrowIfCancellationRequested();
        context.ArmWeeklySettings.Add(setting);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> StaffNamesAsync(IReadOnlyCollection<string> accountIds, CancellationToken cancellationToken)
    {
        var ids = accountIds.Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty).Where(id => id != Guid.Empty).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var accounts = await context.AdminAccounts.AsNoTracking()
            .Where(account => ids.Contains(account.Id))
            .Select(account => new { account.Id, account.StaffName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return accounts.ToDictionary(account => account.Id.ToString("D", CultureInfo.InvariantCulture), account => account.StaffName, StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<WeeklyReportSnapshot>> SnapshotsAsync(IQueryable<WeeklyReport> query, CancellationToken cancellationToken)
    {
        var reports = await query.AsNoTracking()
            .Select(report => new
            {
                report.Id,
                report.PupilId,
                report.ArmId,
                report.WeekNumber,
                report.WeekStartDate,
                report.WeekEndDate,
                report.State,
                report.PublishedAtUtc,
                report.Revision,
                Days = report.Days.Select(day => new
                {
                    day.DayOfWeek,
                    day.ReportDate,
                    day.Behaviour,
                    day.Performance,
                    day.Dressing,
                    day.HomeWork,
                    day.Eating,
                    day.SymptomsOfIllness,
                    day.TeacherComment,
                    day.ParentComment,
                    EditedAt = day.ModifiedAtUtc ?? day.CreatedAtUtc,
                    EditedBy = day.ModifiedBy ?? day.CreatedBy,
                }).ToList(),
            })
            .OrderBy(report => report.WeekNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = await StaffNamesAsync(
            reports.SelectMany(report => report.Days).Select(day => day.EditedBy).OfType<string>().Distinct(StringComparer.Ordinal).ToList(),
            cancellationToken).ConfigureAwait(false);

        return reports
            .Select(report => new WeeklyReportSnapshot(
                report.Id, report.PupilId, report.ArmId, report.WeekNumber, report.WeekStartDate, report.WeekEndDate, report.State,
                report.PublishedAtUtc, report.Revision,
                report.Days
                    .OrderBy(day => day.DayOfWeek)
                    .Select(day =>
                    {
                        string?[] notes = [day.Behaviour, day.Performance, day.Dressing, day.HomeWork, day.Eating, day.SymptomsOfIllness, day.TeacherComment, day.ParentComment];
                        var written = notes.Any(note => note is not null);
                        return new WeeklyDaySnapshot(
                            day.DayOfWeek, day.ReportDate, notes,
                            written ? day.EditedAt : null,
                            written ? day.EditedBy : null,
                            written && day.EditedBy is { } editor ? names.GetValueOrDefault(editor) : null);
                    })
                    .ToList()))
            .ToList();
    }
}
