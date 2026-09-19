using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="AttendanceEntry"/> (spec 09 §6.7.3; TASK-0086 stage A).</summary>
internal sealed class AttendanceEntryConfiguration : IEntityTypeConfiguration<AttendanceEntry>
{
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AttendanceEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("attendance_entry");

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();

        builder.Property(entry => entry.ResultSetId).IsRequired();
        builder.Property(entry => entry.PupilId).IsRequired();
        builder.Property(entry => entry.TimesPresent).IsRequired();

        builder.Property(entry => entry.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(entry => entry.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // One entry per pupil per result set — no void concept here, same shape TraitRatingConfiguration uses.
        builder.HasIndex(entry => new { entry.ResultSetId, entry.PupilId })
            .IsUnique()
            .HasDatabaseName("ix_attendance_entry_result_set_pupil_unique");

        // RESTRICT, same defensive default the rest of this module uses.
        builder.HasOne<ResultSet>()
            .WithMany()
            .HasForeignKey(entry => entry.ResultSetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Pupils.Pupil>()
            .WithMany()
            .HasForeignKey(entry => entry.PupilId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
