using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Weekly;

/// <summary>
/// Spec 6.10.8's auto-publish option: for every arm that has it on, publish the week at 17:00 Lagos time on its Friday.
/// Runs every 15 minutes and catches up until the Monday, so a host that was down at 17:00 still publishes. Each arm's
/// week is handled once: a teacher who unpublishes afterwards is not overridden on the next run. An empty week is left
/// alone, exactly as a manual publish would refuse it.
/// </summary>
internal sealed partial class WeeklyAutoPublishService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<WeeklyAutoPublishService> logger) : BackgroundService
{
    /// <summary>The audit action for an automatic publish; the actor is null.</summary>
    public const string Action = "weekly.auto_publish";

    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LagosOffset = TimeSpan.FromHours(1);
    private static readonly TimeOnly PublishTime = new(17, 0);

    /// <summary>The Monday of the week due for automatic publishing at <paramref name="now"/>, or null outside Friday 17:00 to Sunday.</summary>
    public static DateOnly? DueWeekStart(DateTimeOffset now)
    {
        var lagos = now.ToOffset(LagosOffset);
        var today = DateOnly.FromDateTime(lagos.DateTime);
        var daysSinceFriday = ((int)today.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
        if (daysSinceFriday > 2 || (daysSinceFriday == 0 && TimeOnly.FromDateTime(lagos.DateTime) < PublishTime))
        {
            return null;
        }

        return today.AddDays(-daysSinceFriday - 4);
    }

    /// <summary>One pass. Returns how many arms had a week published.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (DueWeekStart(now) is not { } weekStart)
        {
            return 0;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<ISystemAuditSink>();

        var settings = await context.ArmWeeklySettings
            .Where(setting => setting.AutoPublish && (setting.LastAutoPublishedWeekStart == null || setting.LastAutoPublishedWeekStart != weekStart))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var published = 0;
        foreach (var setting in settings)
        {
            var reports = await context.WeeklyReports
                .Include(report => report.Days)
                .Where(report => report.ArmId == setting.ArmId && report.WeekStartDate == weekStart)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            setting.MarkAutoPublished(weekStart);
            if (!reports.Exists(report => report.HasContent))
            {
                continue;
            }

            foreach (var report in reports)
            {
                report.Publish(now, actor: null);
            }

            await audit.RecordAsync(
                Action,
                "weekly_report",
                entityId: null,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["armId"] = setting.ArmId.ToString("D", CultureInfo.InvariantCulture),
                    ["weekStart"] = weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["reports"] = reports.Count,
                },
                actorAdminId: null,
                cancellationToken)
                .ConfigureAwait(false);
            published++;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return published;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRunDelay, timeProvider, stoppingToken).ConfigureAwait(false);
            using var timer = new PeriodicTimer(Interval, timeProvider);
            do
            {
                try
                {
                    var published = await RunOnceAsync(stoppingToken).ConfigureAwait(false);
                    if (published > 0)
                    {
                        LogRun(logger, published);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogFailure(logger, exception);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Weekly auto-publish: published {Arms} arm week(s).")]
    private static partial void LogRun(ILogger logger, int arms);

    [LoggerMessage(Level = LogLevel.Error, Message = "Weekly auto-publish failed; it will retry on the next run.")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
