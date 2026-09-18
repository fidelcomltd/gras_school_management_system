using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="ResultSet"/> (spec 09 §6.7.3; TASK-0076).</summary>
internal sealed class ResultSetConfiguration : IEntityTypeConfiguration<ResultSet>
{
    private const int AuditActorMaxLength = 128;
    private const int StateMaxLength = 30;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ResultSet> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("result_set");

        builder.HasKey(resultSet => resultSet.Id);
        builder.Property(resultSet => resultSet.Id).ValueGeneratedNever();

        builder.Property(resultSet => resultSet.ArmId).IsRequired();
        builder.Property(resultSet => resultSet.TermId).IsRequired();

        builder.Property(resultSet => resultSet.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(StateMaxLength);

        builder.Property(resultSet => resultSet.NeedsRecompute).IsRequired();
        builder.Property(resultSet => resultSet.RevisionNumber).IsRequired();

        builder.Property(resultSet => resultSet.ReturnReason).HasMaxLength(500);

        // Raw JSON text, same convention as ConfigVersion.SnapshotJson / AuditEvent.BeforeJson.
        builder.Property(resultSet => resultSet.ConfigSnapshotJson).HasColumnType("jsonb");

        builder.Property(resultSet => resultSet.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(resultSet => resultSet.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Spec 6.7.3: "Unique together with term_id. One result set per arm per term."
        builder.HasIndex(resultSet => new { resultSet.ArmId, resultSet.TermId })
            .IsUnique()
            .HasDatabaseName("ix_result_set_arm_id_term_id_unique");

        // Spec 6.3.6's term-close precondition scans every result set in a term by state.
        builder.HasIndex(resultSet => new { resultSet.TermId, resultSet.State })
            .HasDatabaseName("ix_result_set_term_id_state");

        // RESTRICT: no delete route exists for an arm or a term once a result set references it
        // (spec 6.3.6/6.4.7), same defensive default every other FK in this module uses.
        builder.HasOne<Domain.Classes.Arm>()
            .WithMany()
            .HasForeignKey(resultSet => resultSet.ArmId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Sessions.Term>()
            .WithMany()
            .HasForeignKey(resultSet => resultSet.TermId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
