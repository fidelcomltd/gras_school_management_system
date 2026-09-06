using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One row in the global, append-only configuration-version ledger (spec 6.2.9). Every successful
/// save to a versioned settings group writes exactly one of these; nothing is ever updated or
/// deleted — the append-only guarantee is TASK-0005a's central acceptance criterion.
/// </summary>
/// <remarks>
/// <para>
/// ONE GLOBAL LEDGER, NOT ONE CHAIN PER GROUP. <see cref="VersionNumber"/> is monotonic across every
/// group, present and future — identity, then a future abbreviation save, then identity again
/// produces version numbers 1, 2, 3, not two interleaved per-group sequences. It is a real PostgreSQL
/// <c>GENERATED ALWAYS AS IDENTITY</c> column (<c>ConfigVersionConfiguration</c>), not an
/// application-computed "read max, add one" value — the latter has a genuine race under concurrent
/// saves that an identity column does not.
/// </para>
/// <para>
/// <see cref="SnapshotJson"/> holds the WHOLE serialised configuration as of this save — every
/// group's current values, not only the one that changed (6.2.9's rationale, confirmed as proposed in
/// the approved delta: a denormalised copy beats a version reference). Do not optimise this into a
/// reference to live rows; a later migration or a reused version number would then silently corrupt
/// history that this table exists specifically to make immune to that.
/// </para>
/// <para>
/// NOT an <see cref="IAuditableEntity"/> and not <see cref="ISoftDeletable"/> — this row IS the audit
/// record for its own creation (<see cref="ActorAdminId"/>, <see cref="CreatedAtUtc"/>), and it is
/// never soft-deleted (retention/purge, if any, is a later concern, not this card's). Mirrors
/// <c>IdempotencyRecord</c>'s reasoning for the same omission.
/// </para>
/// </remarks>
public sealed class ConfigVersion : Entity<Guid>
{
    private ConfigVersion(
        Guid id,
        string snapshotJson,
        ConfigVersionGroup changedGroup,
        string? actorAdminId,
        string? reason,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        SnapshotJson = snapshotJson;
        ChangedGroup = changedGroup;
        ActorAdminId = actorAdminId;
        Reason = reason;
        CreatedAtUtc = createdAtUtc;
    }

    // EF Core materialisation constructor.
    private ConfigVersion()
        : base()
    {
        SnapshotJson = null!;
    }

    /// <summary>
    /// Globally monotonic across every group. Database-generated (identity column) — never set by
    /// application code, so this is 0 on an entity not yet inserted.
    /// </summary>
    public long VersionNumber { get; private set; }

    /// <summary>The whole serialised configuration as of this save, as a JSON document (stored <c>jsonb</c>).</summary>
    public string SnapshotJson { get; private set; }

    /// <summary>Which settings group this save changed.</summary>
    public ConfigVersionGroup ChangedGroup { get; private set; }

    /// <summary>
    /// The acting administrator's id, or <see langword="null"/> for a system-initiated write (there
    /// is no such writer for this group yet; the field exists for parity with the audit seam's own
    /// "null means system" convention).
    /// </summary>
    public string? ActorAdminId { get; private set; }

    /// <summary>
    /// Populated only when the changed group's own rule requires a reason (6.2.10) — identity does
    /// not, so this is always <see langword="null"/> for a <see cref="ConfigVersionGroup.Identity"/>
    /// row written by TASK-0005a. TASK-0005c's abbreviation save is mandatory-reason and populates it.
    /// </summary>
    public string? Reason { get; private set; }

    /// <summary>When this version was written. <c>TimeProvider</c>-sourced, never <c>DateTimeOffset.UtcNow</c>.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Creates a new, not-yet-persisted version row. The winning save only — a rejected stale save never calls this.</summary>
    public static ConfigVersion Create(
        Guid id,
        string snapshotJson,
        ConfigVersionGroup changedGroup,
        string? actorAdminId,
        string? reason,
        DateTimeOffset now) =>
        new(id, snapshotJson, changedGroup, actorAdminId, reason, now);
}
