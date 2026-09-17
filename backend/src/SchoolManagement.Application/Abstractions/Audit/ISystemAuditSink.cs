namespace SchoolManagement.Application.Abstractions.Audit;

/// <summary>
/// Records an audit event against the real, append-only <c>audit_event</c> table (spec 6.1.12,
/// spec 14 §9.3). TASK-0048 replaced the previous log-only seam
/// (<c>LoggingSystemAuditSink</c>, DELETED) with real persistence — see
/// <c>Infrastructure/Audit/SystemAuditSink</c>.
/// </summary>
/// <remarks>
/// <para>
/// TWO METHODS, ONE FOR EACH OUTCOME, RATHER THAN AN <c>outcome</c> PARAMETER ON ONE: the two
/// outcomes are durable in entirely different ways (root <c>CLAUDE.md</c> §4.1 decision,
/// 2026-09-09). <see cref="RecordAsync"/> joins the AMBIENT unit-of-work transaction — the same
/// transaction as the change it records, no <c>SaveChangesAsync</c> of its own — because a
/// successful command's write and its audit row must commit or roll back together.
/// <see cref="RecordRejectionAsync"/> commits IMMEDIATELY on its own short-lived connection,
/// because <c>UnitOfWork.ExecuteAtomicallyAsync</c> rolls back the ambient transaction whenever the
/// command's <c>Result</c> is a failure — and a rejection record must survive exactly that
/// rollback, or every escalation-rule and privilege-check rejection this seam exists to prove
/// would be silently lost.
/// </para>
/// <para>
/// Existing callers keep calling whichever of the two methods they already called — most call
/// sites need no change at all. <c>reason</c> is additive (default <see langword="null"/>) so the
/// one call site that has a real value (<c>ChangeAdminAccountStatusCommandHandler</c>, spec
/// 6.1.12's admin-deactivation reason) can pass it without touching any other caller's argument
/// list.
/// </para>
/// </remarks>
public interface ISystemAuditSink
{
    /// <summary>
    /// Records a SUCCESSFUL system-initiated or admin-attributed audit event, joining the ambient
    /// transaction (see the interface remarks).
    /// </summary>
    /// <param name="action">The audit action code, for example <c>system.idempotency_purge</c>.</param>
    /// <param name="entityType">The kind of entity affected, or <see langword="null"/>.</param>
    /// <param name="entityId">
    /// The specific entity affected, or <see langword="null"/> for a bulk/batch action — spec
    /// 6.1.12: "Null for bulk actions, which instead carry a batch id in metadata."
    /// </param>
    /// <param name="metadata">Additional structured detail, safe to log (never PII).</param>
    /// <param name="actorAdminId">
    /// The acting administrator's id, or <see langword="null"/> for a genuine system-initiated
    /// action (a background job, a migration) — the same "null means system, never a fabricated
    /// string" convention <c>ICurrentUser.UserId</c> already documents.
    /// </param>
    /// <param name="cancellationToken">Propagated to any underlying write.</param>
    /// <param name="reason">
    /// Spec 6.1.12's mandatory reason for the actions it lists, or <see langword="null"/>. Optional
    /// and trailing so every pre-existing call site is unaffected.
    /// </param>
    /// <param name="beforeMetadata">
    /// The prior state of the fields this action changed — becomes <c>AuditEvent.BeforeJson</c>, the
    /// same way <paramref name="metadata"/> becomes <c>AfterJson</c> (2026-09-09's standing
    /// obligation: "before_json is never populated... every card writing an audited mutation must
    /// populate both"). <see langword="null"/> on a pure create, where there is no prior state.
    /// Optional and trailing so every pre-existing call site is unaffected.
    /// </param>
    Task RecordAsync(
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        string? actorAdminId,
        CancellationToken cancellationToken,
        string? reason = null,
        IReadOnlyDictionary<string, object?>? beforeMetadata = null);

    /// <summary>
    /// Records a REJECTED attempt (an escalation-rule refusal) — durable even though the command
    /// that triggered it returns a failure <see cref="SchoolManagement.Domain.Common.Result"/> and
    /// the ambient transaction rolls back. Same parameter shape as <see cref="RecordAsync"/>; only
    /// the durability mechanism differs.
    /// </summary>
    Task RecordRejectionAsync(
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        string? actorAdminId,
        CancellationToken cancellationToken,
        string? reason = null);
}
