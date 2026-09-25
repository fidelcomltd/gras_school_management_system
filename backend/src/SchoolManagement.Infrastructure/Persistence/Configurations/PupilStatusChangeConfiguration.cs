using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="PupilStatusChange"/> (spec 6.5.14).</summary>
internal sealed class PupilStatusChangeConfiguration : IEntityTypeConfiguration<PupilStatusChange>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PupilStatusChange> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("pupil_status_changes");

        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).ValueGeneratedNever();

        builder.Property(row => row.PupilId).IsRequired();
        builder.Property(row => row.FromStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(row => row.ToStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(row => row.EffectiveDate).IsRequired();
        builder.Property(row => row.Reason).HasMaxLength(PupilStatusChange.ReasonMaxLength);
        builder.Property(row => row.ArmId);
        builder.Property(row => row.ChangedBy);
        builder.Property(row => row.ChangedAtUtc).IsRequired();

        // RESTRICT, as PupilRegNumberHistoryConfiguration: an approved pupil is never deleted (spec 6.5.16).
        builder.HasOne<Pupil>()
            .WithMany()
            .HasForeignKey(row => row.PupilId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Arm>()
            .WithMany()
            .HasForeignKey(row => row.ArmId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(row => row.PupilId).HasDatabaseName("ix_pupil_status_changes_pupil_id");

        // No foreign key for ChangedBy, the same reasoning AuditEventConfiguration gives for its actor.
    }
}
