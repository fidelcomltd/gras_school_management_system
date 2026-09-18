using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Audit;

/// <summary>
/// One row of the append-only audit trail (spec 6.1.12, spec 14 §9.3). TASK-0048: the real
/// persistence that replaces the two <c>Logging*AuditSink</c> log-only seams.
/// </summary>
/// <remarks>
/// <para>
/// NOT an <see cref="IAuditableEntity"/> and not <see cref="ISoftDeletable"/>: this row is never
/// updated and never deleted through the application (spec 9.4: "audit_event | Never | Nothing.
/// Pruned only by the retention schedule."), so neither the created/modified stamping convention
/// nor the soft-delete rewrite applies. <see cref="OccurredAt"/> is this row's own, permanent
/// timestamp — there is no second one to maintain.
/// </para>
/// <para>
/// <c>Id</c> is <see langword="long"/>, not the <see cref="Guid"/> v7 the rest of the schema
/// uses. Spec 6.1.12 is explicit: "Monotonic, so ordering is unambiguous even within the same
/// millisecond" — a database identity column (BIGSERIAL), left at EF Core's default
/// value-generation for an integer key rather than assigned by the application.
/// </para>
/// <para>
/// CALLERS MUST TRUNCATE THE SOURCE IP BEFORE calling <see cref="Create"/> — see
/// <see cref="AuditFieldTruncation"/>. This type stores whatever it is given verbatim; the PII rule
/// (spec 9.3, root <c>CLAUDE.md</c> §8) is enforced by the writer, not by this entity, so that the
/// rule is visible at the one place a raw value is still in scope.
/// </para>
/// </remarks>
public sealed class AuditEvent : Entity<long>
{
    private AuditEvent(
        DateTimeOffset occurredAt,
        Guid? actorAdminId,
        string actorLabel,
        string action,
        string entityType,
        string? entityId,
        AuditOutcome outcome,
        string? beforeJson,
        string? afterJson,
        string? reason,
        string? sourceIp,
        string? userAgent)
        : base(0)
    {
        OccurredAt = occurredAt;
        ActorAdminId = actorAdminId;
        ActorLabel = actorLabel;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Outcome = outcome;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        Reason = reason;
        SourceIp = sourceIp;
        UserAgent = userAgent;
    }

    // EF Core materialisation constructor.
    private AuditEvent()
        : base()
    {
        ActorLabel = null!;
        Action = null!;
        EntityType = null!;
    }

    /// <summary>Stored UTC, displayed WAT (spec 6.1.12).</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>
    /// Null for a system action (spec 6.1.12: "Null for system actions such as the nightly archive
    /// and for portal lookups").
    /// </summary>
    public Guid? ActorAdminId { get; private set; }

    /// <summary>
    /// Staff name and email captured AT WRITE TIME, so the row stays readable after the account is
    /// later renamed or deleted (spec 6.1.12). Never joined on read.
    /// </summary>
    public string ActorLabel { get; private set; }

    /// <summary>
    /// The privilege string of the operation (for example <c>settings.grading.update</c>), or a
    /// fixed system code such as <c>portal.lookup</c>/<c>system.archive</c> for a non-privileged
    /// actor (spec 6.1.12).
    /// </summary>
    public string Action { get; private set; }

    /// <summary>For example <c>grading_band</c>, <c>result_set</c>, <c>pupil</c> (spec 6.1.12).</summary>
    public string EntityType { get; private set; }

    /// <summary>Null for a bulk action, which carries a batch id in <see cref="AfterJson"/> instead.</summary>
    public string? EntityId { get; private set; }

    /// <summary>Whether the governed write happened, or the attempt was refused.</summary>
    public AuditOutcome Outcome { get; private set; }

    /// <summary>The prior state of changed fields only. Null on create. TASK-0048 adds the column; no handler populates it yet (out of scope — each future module card does, for its own writes).</summary>
    public string? BeforeJson { get; private set; }

    /// <summary>The new state of changed fields only. Null on delete.</summary>
    public string? AfterJson { get; private set; }

    /// <summary>
    /// Mandatory for the actions spec 6.1.12 lists (score void, registration number correction,
    /// result return/unpublication, promotion reversal, pin batch revocation, admin deactivation, a
    /// warned settings edit); null otherwise. Validated by the command, not by this entity.
    /// </summary>
    public string? Reason { get; private set; }

    /// <summary>Already truncated to /24 (IPv4) or /48 (IPv6) by the caller — see <see cref="AuditFieldTruncation"/>.</summary>
    public string? SourceIp { get; private set; }

    /// <summary>Already truncated to 300 characters by the caller.</summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Builds one row. Every value is taken as given — truncation, actor-label resolution and the
    /// reason mandate are the CALLER's responsibility (the writer in <c>Infrastructure/Audit</c>),
    /// because none of those rules can fail in a way this entity would need to reject; there is
    /// nothing here for a <see cref="Result"/> to carry.
    /// </summary>
    public static AuditEvent Create(
        DateTimeOffset occurredAt,
        Guid? actorAdminId,
        string actorLabel,
        string action,
        string entityType,
        string? entityId,
        AuditOutcome outcome,
        string? beforeJson,
        string? afterJson,
        string? reason,
        string? sourceIp,
        string? userAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        return new AuditEvent(
            occurredAt,
            actorAdminId,
            actorLabel,
            action,
            entityType,
            entityId,
            outcome,
            beforeJson,
            afterJson,
            reason,
            sourceIp,
            userAgent);
    }
}
