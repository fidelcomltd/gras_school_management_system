using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Domain.Audit;

namespace SchoolManagement.Infrastructure.Audit;

/// <summary>
/// Real, persisted <see cref="ISystemAuditSink"/> (spec 6.1.12, spec 14 §9.3). TASK-0048 replaces
/// <c>LoggingSystemAuditSink</c> (DELETED, no longer registered anywhere).
/// </summary>
/// <remarks>
/// See the interface's own remarks for why success and rejection are two methods rather than one
/// method with an outcome flag: they persist through entirely different mechanisms.
/// </remarks>
internal sealed class SystemAuditSink(
    IAuditEventRepository auditEvents,
    AuditEventFactory factory,
    RejectedAuditEventWriter rejectedWriter)
    : ISystemAuditSink
{
    /// <inheritdoc />
    public async Task RecordAsync(
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        string? actorAdminId,
        CancellationToken cancellationToken,
        string? reason = null)
    {
        var auditEvent = await factory
            .BuildAsync(AuditOutcome.Success, action, entityType, entityId, metadata, actorAdminId, reason, cancellationToken)
            .ConfigureAwait(false);

        // No SaveChangesAsync: joins the ambient DbContext's change tracker, committed by
        // UnitOfWorkBehavior alongside the change this event records.
        await auditEvents.AddAsync(auditEvent, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RecordRejectionAsync(
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        string? actorAdminId,
        CancellationToken cancellationToken,
        string? reason = null)
    {
        var auditEvent = await factory
            .BuildAsync(AuditOutcome.Rejected, action, entityType, entityId, metadata, actorAdminId, reason, cancellationToken)
            .ConfigureAwait(false);

        // Commits immediately, on its own connection — see RejectedAuditEventWriter's remarks.
        await rejectedWriter.WriteAsync(auditEvent, cancellationToken).ConfigureAwait(false);
    }
}
