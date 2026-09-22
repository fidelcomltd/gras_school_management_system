using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Pins;

/// <summary>
/// Spec 6.8.4 and 6.8.10's nightly job. It deletes pin ciphertext at each batch's purge date, after which a pin value
/// exists nowhere in the system. It also marks a batch Exhausted once its session has closed or none of its pins can
/// still be used. Both are set-based updates, safe to run any number of times.
/// </summary>
internal sealed partial class PinMaintenanceService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<PinMaintenanceService> logger) : BackgroundService
{
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>One pass. Returns (ciphertexts purged, batches marked exhausted).</summary>
    public async Task<(int Purged, int Exhausted)> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = timeProvider.GetUtcNow();

        var purged = await context.Pins
            .Where(pin => pin.Ciphertext != null && context.PinBatches.Any(batch => batch.Id == pin.BatchId && batch.PlaintextPurgeAtUtc <= now))
            .ExecuteUpdateAsync(setters => setters.SetProperty(pin => pin.Ciphertext, (string?)null), cancellationToken)
            .ConfigureAwait(false);

        var exhausted = await context.PinBatches
            .Where(batch => batch.State != PinBatchState.Revoked && batch.State != PinBatchState.Exhausted)
            .Where(batch =>
                context.Set<AcademicSession>().Any(session => session.Id == batch.SessionId && session.State == SessionState.Closed)
                || !context.Pins.Any(pin => pin.BatchId == batch.Id && (pin.State == PinState.Unused || pin.State == PinState.Active)))
            .ExecuteUpdateAsync(setters => setters.SetProperty(batch => batch.State, PinBatchState.Exhausted), cancellationToken)
            .ConfigureAwait(false);

        return (purged, exhausted);
    }

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
                    var (purged, exhausted) = await RunOnceAsync(stoppingToken).ConfigureAwait(false);
                    LogRun(logger, purged, exhausted);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Pin maintenance: purged {Purged} pin ciphertexts, marked {Exhausted} batches exhausted.")]
    private static partial void LogRun(ILogger logger, int purged, int exhausted);

    [LoggerMessage(Level = LogLevel.Error, Message = "Pin maintenance failed; it will retry on the next run.")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
