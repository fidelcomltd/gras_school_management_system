using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Promotion;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="PromotionBatch"/> (spec 6.3.7).</summary>
internal sealed class PromotionBatchConfiguration : IEntityTypeConfiguration<PromotionBatch>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PromotionBatch> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("promotion_batch");
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.Id).ValueGeneratedNever();
        builder.Property(batch => batch.State).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(batch => batch.CommittedBy).HasMaxLength(128);
        builder.Property(batch => batch.ReversedBy).HasMaxLength(128);
        builder.Property(batch => batch.ReversalReason).HasMaxLength(PromotionBatch.ReasonMaxLength);
        builder.HasOne<AcademicSession>().WithMany().HasForeignKey(batch => batch.SourceSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AcademicSession>().WithMany().HasForeignKey(batch => batch.TargetSessionId).OnDelete(DeleteBehavior.Restrict);

        // At most one committed batch per source session (spec 6.3.9: promotion run twice); reversed ones are kept beside it.
        builder.HasIndex(batch => batch.SourceSessionId)
            .IsUnique()
            .HasFilter("state = 'Committed'")
            .HasDatabaseName("ix_promotion_batch_one_committed_per_session");

        builder.HasMany(batch => batch.Decisions).WithOne().HasForeignKey(decision => decision.BatchId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(batch => batch.Decisions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Mapping for <see cref="PromotionDecision"/>.</summary>
internal sealed class PromotionDecisionConfiguration : IEntityTypeConfiguration<PromotionDecision>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PromotionDecision> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("promotion_decision");
        builder.HasKey(decision => decision.Id);
        builder.Property(decision => decision.Id).ValueGeneratedNever();
        builder.Property(decision => decision.ProposedOutcome).HasConversion<string>().HasMaxLength(20);
        builder.Property(decision => decision.Outcome).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(decision => decision.Reason).HasMaxLength(PromotionBatch.ReasonMaxLength);
        builder.HasOne<Pupil>().WithMany().HasForeignKey(decision => decision.PupilId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(decision => new { decision.BatchId, decision.PupilId }).IsUnique().HasDatabaseName("ix_promotion_decision_batch_pupil_unique");

        // The enrolments are referenced by id only: reversal deletes the new one, so no foreign key may pin it.
        builder.HasIndex(decision => decision.PupilId).HasDatabaseName("ix_promotion_decision_pupil");
    }
}
