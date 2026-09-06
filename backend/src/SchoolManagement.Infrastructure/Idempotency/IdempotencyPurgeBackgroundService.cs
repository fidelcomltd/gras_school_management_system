using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Idempotency;

namespace SchoolManagement.Infrastructure.Idempotency;

/// <summary>
/// Runs <see cref="IdempotencyPurgeJob"/> on a fixed interval for the lifetime of the host (TASK-0019,
/// §9.9: "a scheduled job performs each purge"). A test that needs a deterministic single run
/// resolves <see cref="IdempotencyPurgeJob"/> directly from a scope instead of waiting on this timer.
/// </summary>
/// <remarks>
/// <para>
/// CATCHES A BROAD EXCEPTION around one cycle — a deliberate, narrow exception to
/// <c>backend/AGENTS.md</c>'s "never catch an exception in a handler": that rule targets request
/// HANDLERS, where the global exception handler owns turning a fault into a response. There is no
/// request here, and no global handler watching this loop — by default an unhandled exception
/// escaping <see cref="BackgroundService.ExecuteAsync"/> takes the ENTIRE host down with it. A
/// transient failure in one purge cycle (a momentary database blip) must not do that to an API that
/// has nothing to do with idempotency purging; it is logged and the loop tries again next interval.
/// </para>
/// <para>
/// A fresh DI scope per cycle: <see cref="IdempotencyPurgeJob"/> and the store/sink behind it are
/// registered <c>Scoped</c> (consistent with every other repository in this codebase), and this
/// service itself is a singleton for the life of the host.
/// </para>
/// </remarks>
internal sealed class IdempotencyPurgeBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<IdempotencyPurgeBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var job = scope.ServiceProvider.GetRequiredService<IdempotencyPurgeJob>();
                var purgedCount = await job.RunOnceAsync(stoppingToken).ConfigureAwait(false);

                IdempotencyPurgeBackgroundServiceLog.Ran(logger, purgedCount);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // See the class remarks: one bad cycle must not take the whole host down.
                IdempotencyPurgeBackgroundServiceLog.Failed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}

/// <summary>Source-generated log messages for <see cref="IdempotencyPurgeBackgroundService"/>.</summary>
internal static partial class IdempotencyPurgeBackgroundServiceLog
{
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Idempotency purge run completed: {PurgedCount} row(s) removed.")]
    public static partial void Ran(ILogger logger, int purgedCount);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Error,
        Message = "Idempotency purge run failed; will retry on the next interval.")]
    public static partial void Failed(ILogger logger, Exception exception);
}
