using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Audit;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="AuditEvent"/> (spec 6.1.12, spec 14 §9.3).</summary>
internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    private const int ActorLabelMaxLength = 160;
    private const int ActionMaxLength = 80;
    private const int EntityTypeMaxLength = 60;
    private const int EntityIdMaxLength = 60;
    private const int ReasonMaxLength = 500;
    private const int SourceIpMaxLength = 45;
    private const int UserAgentMaxLength = AuditFieldTruncation.UserAgentMaxLength;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("audit_event");

        builder.HasKey(auditEvent => auditEvent.Id);

        // EF Core's default for an integer key is already ValueGeneratedOnAdd; stated explicitly
        // here (rather than left implicit) because this is the first BIGSERIAL-identity key in the
        // schema — every other entity assigns its own Guid v7 — and spec 6.1.12 is explicit that
        // this one column must NOT follow that convention: "Not a GUID... wants it monotonic".
        builder.Property(auditEvent => auditEvent.Id).ValueGeneratedOnAdd();

        builder.Property(auditEvent => auditEvent.OccurredAt).IsRequired();

        builder.Property(auditEvent => auditEvent.ActorAdminId);

        builder.Property(auditEvent => auditEvent.ActorLabel)
            .IsRequired()
            .HasMaxLength(ActorLabelMaxLength);

        builder.Property(auditEvent => auditEvent.Action)
            .IsRequired()
            .HasMaxLength(ActionMaxLength);

        builder.Property(auditEvent => auditEvent.EntityType)
            .IsRequired()
            .HasMaxLength(EntityTypeMaxLength);

        builder.Property(auditEvent => auditEvent.EntityId)
            .HasMaxLength(EntityIdMaxLength);

        builder.Property(auditEvent => auditEvent.Outcome)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // Unbounded: a JSON snapshot is not a "name"-shaped column, and the default 256-char
        // convention (ApplicationDbContext.ConfigureConventions) would silently truncate it — same
        // reasoning as IdempotencyRecordConfiguration.ResponseBodyJson.
        builder.Property(auditEvent => auditEvent.BeforeJson).HasColumnType("jsonb");
        builder.Property(auditEvent => auditEvent.AfterJson).HasColumnType("jsonb");

        builder.Property(auditEvent => auditEvent.Reason).HasMaxLength(ReasonMaxLength);

        builder.Property(auditEvent => auditEvent.SourceIp).HasMaxLength(SourceIpMaxLength);

        builder.Property(auditEvent => auditEvent.UserAgent).HasMaxLength(UserAgentMaxLength);

        // The read surface (TASK-0049) filters by date range, actor, action, entity type and
        // outcome, newest first by default — this index supports the default ordering and the
        // date-range filter together, the pair every other filter combination narrows further.
        builder.HasIndex(auditEvent => auditEvent.OccurredAt)
            .HasDatabaseName("ix_audit_event_occurred_at");

        builder.HasIndex(auditEvent => auditEvent.ActorAdminId)
            .HasDatabaseName("ix_audit_event_actor_admin_id");

        builder.HasIndex(auditEvent => auditEvent.Outcome)
            .HasDatabaseName("ix_audit_event_outcome");

        // Deliberately NO HasOne/foreign key to AdminAccount: spec 6.1.12 requires the row to stay
        // readable after the account is renamed OR DELETED, and actor_label already carries
        // everything a join would have provided. A foreign key here would also conflict with
        // "Null for system actions" the moment retention (§9.8) ever needed to archive rows whose
        // actor no longer exists.
    }
}
