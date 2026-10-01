using Microsoft.Extensions.Logging;
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
    RejectedAuditEventWriter rejectedWriter,
    ILogger<SystemAuditSink> logger)
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
        string? reason = null,
        IReadOnlyDictionary<string, object?>? beforeMetadata = null)
    {
        var auditEvent = await factory
            .BuildAsync(AuditOutcome.Success, action, entityType, entityId, metadata, actorAdminId, reason, cancellationToken, beforeMetadata)
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
        // Commits immediately, on its own connection — see RejectedAuditEventWriter's remarks. A failed write is logged,
        // never thrown: the caller's refusal stands (TASK-0058).
        await RejectedAuditWrite
            .WriteOrLogAsync(factory, rejectedWriter, logger, action, entityType, entityId, metadata, actorAdminId, reason, cancellationToken)
            .ConfigureAwait(false);
    }
}
