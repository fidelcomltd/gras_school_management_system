namespace SchoolManagement.Application.Abstractions.Audit;

/// <summary>
/// Records a system-initiated action against the audit trail (spec 6.1.12: "actor_admin_id... Null
/// for system actions such as the nightly archive"). First caller: the idempotency retention purge
/// (TASK-0019, §9.9), which must record what it removed on every run.
/// </summary>
/// <remarks>
/// A SEAM, the same shape as <c>IAuthorizationAuditSink</c> (TASK-0002) and for the same reason: the
/// real <c>audit_event</c> table (spec 6.1.12) does not exist yet, so the registered implementation
/// (<c>LoggingSystemAuditSink</c>) writes a structured log entry rather than a persisted row.
/// Whichever future card builds real <c>audit_event</c> persistence replaces both seams together,
/// not just this one.
/// </remarks>
public interface ISystemAuditSink
{
    /// <summary>Records one system-initiated audit event.</summary>
    /// <param name="action">The audit action code, for example <c>system.idempotency_purge</c>.</param>
    /// <param name="entityType">The kind of entity affected, or <see langword="null"/>.</param>
    /// <param name="entityId">
    /// The specific entity affected, or <see langword="null"/> for a bulk/batch action — spec
    /// 6.1.12: "Null for bulk actions, which instead carry a batch id in metadata."
    /// </param>
    /// <param name="metadata">Additional structured detail, safe to log (never PII).</param>
    /// <param name="cancellationToken">Propagated to any underlying write.</param>
    Task RecordAsync(
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        CancellationToken cancellationToken);
}
