using Microsoft.Extensions.Logging;
using SchoolManagement.Domain.Audit;

namespace SchoolManagement.Infrastructure.Audit;

/// <summary>
/// Writes a rejection audit row and never lets the WRITE's failure change the refusal (TASK-0058, human ruling
/// 2026-09-19: fail open). The row commits on its own connection (<see cref="RejectedAuditEventWriter"/>), so it can fail
/// while the request is fine; before this, that failure turned a 403 into a 500, telling the caller the endpoint exists
/// and hiding the denial. Building the event is NOT covered: it reads through the request's own context and validates
/// the row, and a failure there is a bug or a request-level fault that must surface as before.
/// </summary>
/// <remarks>
/// The write runs with <see cref="CancellationToken.None"/>: a caller who disconnects mid-refusal must not be able to
/// stop the row being written. A failed write is logged at error level with every column of the row, so it can be
/// rebuilt by hand; that is exactly what the audit table would have held, at the same sensitivity.
/// </remarks>
internal static partial class RejectedAuditWrite
{
    /// <summary>Builds the row, then writes it; a failed write is logged instead of thrown.</summary>
    public static async Task WriteOrLogAsync(
        AuditEventFactory factory,
        RejectedAuditEventWriter writer,
        ILogger logger,
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        string? actorAdminId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var auditEvent = await factory
            .BuildAsync(AuditOutcome.Rejected, action, entityType, entityId, metadata, actorAdminId, reason, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await writer.WriteAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var actorAdminIdText = auditEvent.ActorAdminId?.ToString() ?? "(none)";
            RejectionNotWritten(
                logger,
                exception,
                auditEvent.Action,
                auditEvent.EntityType,
                auditEvent.EntityId ?? "(none)",
                actorAdminIdText,
                auditEvent.ActorLabel,
                auditEvent.Reason ?? "(none)",
                auditEvent.AfterJson ?? "(none)",
                auditEvent.SourceIp ?? "(none)",
                auditEvent.UserAgent ?? "(none)",
                auditEvent.OccurredAt);
        }
    }

    [LoggerMessage(
        EventId = 3100,
        Level = LogLevel.Error,
        Message = "Rejection audit row NOT WRITTEN; the refusal stands. Rebuild it from: action {Action}, entity {EntityType}/{EntityId}, " +
            "actor {ActorAdminId} ({ActorLabel}), reason {Reason}, metadata {Metadata}, source {SourceIp}, agent {UserAgent}, " +
            "occurred {OccurredAt:O}")]
    private static partial void RejectionNotWritten(
        ILogger logger,
        Exception exception,
        string action,
        string entityType,
        string entityId,
        string actorAdminId,
        string actorLabel,
        string reason,
        string metadata,
        string sourceIp,
        string userAgent,
        DateTimeOffset occurredAt);
}
