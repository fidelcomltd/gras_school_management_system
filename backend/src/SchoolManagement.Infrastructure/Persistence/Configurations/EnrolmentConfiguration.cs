using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="Enrolment"/> (spec 02-data-model.md §5.1, §5.2).</summary>
internal sealed class EnrolmentConfiguration : IEntityTypeConfiguration<Enrolment>
{
    /// <summary>Maximum length of the stored audit-actor column, matching <c>PupilConfiguration</c>.</summary>
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Enrolment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("enrolments");

        builder.HasKey(enrolment => enrolment.Id);
        builder.Property(enrolment => enrolment.Id).ValueGeneratedNever();

        builder.Property(enrolment => enrolment.PupilId).IsRequired();
        builder.Property(enrolment => enrolment.ArmId).IsRequired();

        builder.Property(enrolment => enrolment.EffectiveFrom).IsRequired().HasColumnType("date");
        builder.Property(enrolment => enrolment.EffectiveTo).HasColumnType("date");

        builder.Property(enrolment => enrolment.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(enrolment => enrolment.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // THE database enforcement of spec 02 §5.2's "a pupil has exactly one open enrolment at any
        // moment" — a partial unique index, not only a domain-level check, because this entity
        // cannot see a sibling row for the same pupil to check against. Every closed row is
        // excluded from the index by the filter, so a pupil may accumulate any number of them.
        builder.HasIndex(enrolment => enrolment.PupilId)
            .IsUnique()
            .HasFilter("effective_to IS NULL")
            .HasDatabaseName("ix_enrolments_pupil_id_open_unique");

        // Spec 06 §6.4.6's capacity count and the arm-of-record lookup (spec 02 §5.2) both filter on
        // (arm_id, open-only) — this index serves both without a table scan.
        builder.HasIndex(enrolment => new { enrolment.ArmId, enrolment.EffectiveTo })
            .HasDatabaseName("ix_enrolments_arm_id_effective_to");

        // RESTRICT, same defensive default ArmConfiguration uses for its own references: no delete
        // route exists for a pupil or an arm once enrolled (spec 06 §6.4.7: an arm with any
        // enrolment can only be deactivated), so this never fires in practice today.
        builder.HasOne<Pupil>()
            .WithMany()
            .HasForeignKey(enrolment => enrolment.PupilId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Arm>()
            .WithMany()
            .HasForeignKey(enrolment => enrolment.ArmId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
