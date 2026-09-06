using SchoolManagement.Application.Abstractions.Audit;

namespace SchoolManagement.Application.Idempotency;

/// <summary>
/// Runs the idempotency retention purge (TASK-0019, §9.9: "an indefinite table is a defect, not a
/// deferral") and records it on the audit trail. Called on a schedule by
/// <c>SchoolManagement.Infrastructure.Idempotency.IdempotencyPurgeBackgroundService</c>, and directly
/// by tests that need a deterministic single run rather than waiting on a timer.
/// </summary>
/// <remarks>
/// Plain Application-layer class rather than a mediator <c>ICommand</c>: nothing here is dispatched
/// from an HTTP request, there is no caller to validate input against, and forcing it through
/// <c>ISender</c> would buy nothing but an awkward fit — the mediator pipeline's stages (request
/// logging keyed by request TYPE, unit-of-work keyed by a single transaction) are shaped around one
/// HTTP-triggered write, not a scheduled maintenance sweep with its own row count to report.
/// </remarks>
public sealed class IdempotencyPurgeJob(
    IIdempotencyStore store,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
{
    /// <summary>The audit action code recorded for every purge run (§9.9, spec 6.1.12).</summary>
    public const string PurgeAction = "system.idempotency_purge";

    /// <summary>The audit entity type recorded for every purge run.</summary>
    public const string PurgeEntityType = "idempotency_record";

    /// <summary>
    /// Purges every row past its retention window and records one audit event for the run —
    /// unconditionally, even when nothing was purged, so the run itself is on the trail and a gap in
    /// the log reads as "the job did not run" rather than being ambiguous with "it ran and found
    /// nothing".
    /// </summary>
    /// <returns>The number of rows removed.</returns>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var purgedCount = await store.PurgeExpiredAsync(now, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            PurgeAction,
            PurgeEntityType,
            entityId: null, // Bulk action — spec 6.1.12's "Null for bulk actions" case.
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["count"] = purgedCount },
            cancellationToken).ConfigureAwait(false);

        return purgedCount;
    }
}
