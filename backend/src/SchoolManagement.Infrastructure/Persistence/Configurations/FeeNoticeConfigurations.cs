using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Fees;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="FeeLabel"/> (spec 6.2.13).</summary>
internal sealed class FeeLabelConfiguration : IEntityTypeConfiguration<FeeLabel>
{
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FeeLabel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("fee_label");
        builder.HasKey(label => label.Id);
        builder.Property(label => label.Id).ValueGeneratedNever();
        builder.Property(label => label.Label).IsRequired().HasMaxLength(FeeLabel.LabelMaxLength);
        builder.Property(label => label.Kind).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(label => label.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(label => label.ModifiedBy).HasMaxLength(AuditActorMaxLength);
        builder.HasOne<Section>().WithMany().HasForeignKey(label => label.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(label => label.SectionId).HasDatabaseName("ix_fee_label_section");

        // At most one outstanding line per section: it is the one per-pupil figure on the sheet.
        builder.HasIndex(label => label.SectionId)
            .IsUnique()
            .HasFilter("kind = 'Outstanding'")
            .HasDatabaseName("ix_fee_label_one_outstanding_per_section");
    }
}

/// <summary>Mapping for <see cref="FeeAmount"/>.</summary>
internal sealed class FeeAmountConfiguration : IEntityTypeConfiguration<FeeAmount>
{
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FeeAmount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("fee_amount");
        builder.HasKey(amount => amount.Id);
        builder.Property(amount => amount.Id).ValueGeneratedNever();
        builder.Property(amount => amount.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(amount => amount.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Removing a line takes its amounts with it; nothing else deletes them.
        builder.HasOne<FeeLabel>().WithMany().HasForeignKey(amount => amount.FeeLabelId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Term>().WithMany().HasForeignKey(amount => amount.TermId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClassLevel>().WithMany().HasForeignKey(amount => amount.ClassLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(amount => new { amount.TermId, amount.ClassLevelId, amount.FeeLabelId })
            .IsUnique()
            .HasDatabaseName("ix_fee_amount_term_level_label_unique");
        builder.HasIndex(amount => amount.FeeLabelId).HasDatabaseName("ix_fee_amount_label");
    }
}

/// <summary>Mapping for <see cref="OutstandingFee"/>.</summary>
internal sealed class OutstandingFeeConfiguration : IEntityTypeConfiguration<OutstandingFee>
{
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutstandingFee> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("outstanding_fee");
        builder.HasKey(fee => fee.Id);
        builder.Property(fee => fee.Id).ValueGeneratedNever();
        builder.Property(fee => fee.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(fee => fee.ModifiedBy).HasMaxLength(AuditActorMaxLength);
        builder.HasOne<ResultSet>().WithMany().HasForeignKey(fee => fee.ResultSetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Pupil>().WithMany().HasForeignKey(fee => fee.PupilId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(fee => new { fee.ResultSetId, fee.PupilId })
            .IsUnique()
            .HasDatabaseName("ix_outstanding_fee_result_set_pupil_unique");
        builder.HasIndex(fee => fee.PupilId).HasDatabaseName("ix_outstanding_fee_pupil");
    }
}
