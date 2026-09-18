using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="AdmissionRecord"/> (spec 6.5.9).</summary>
internal sealed class AdmissionRecordConfiguration : IEntityTypeConfiguration<AdmissionRecord>
{
    /// <summary>Maximum length of the stored audit-actor column, matching <c>PupilConfiguration</c>.</summary>
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AdmissionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("admission_records");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();

        builder.Property(record => record.PupilId).IsRequired();
        builder.Property(record => record.SessionId).IsRequired();

        builder.Property(record => record.DateApplicationReceived).HasColumnType("date");
        builder.Property(record => record.DateAdmitted).IsRequired().HasColumnType("date");

        builder.Property(record => record.ClassAdmittedInto).IsRequired();

        builder.Property(record => record.AdmissionType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(record => record.AdmissionTypeNote).HasMaxLength(AdmissionRecord.AdmissionTypeNoteMaxLength);

        builder.Property(record => record.AssessmentRequired).IsRequired();
        builder.Property(record => record.AssessmentResultRemarks).HasMaxLength(AdmissionRecord.AssessmentResultRemarksMaxLength);

        builder.Property(record => record.DeclarationName).HasMaxLength(AdmissionRecord.DeclarationNameMaxLength);
        builder.Property(record => record.DeclarationSigned).IsRequired();
        builder.Property(record => record.DeclarationDate).HasColumnType("date");

        builder.Property(record => record.HeadOfSchoolConfirmed).IsRequired();
        builder.Property(record => record.HeadOfSchoolName).HasMaxLength(AdmissionRecord.HeadOfSchoolNameMaxLength);

        builder.Property(record => record.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(record => record.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // One row per pupil (spec 6.5.9) — the invariant CreatePupilHandler's single transaction
        // relies on, backstopped here rather than trusted to application code alone.
        builder.HasIndex(record => record.PupilId)
            .IsUnique()
            .HasDatabaseName("ix_admission_records_pupil_id_unique");

        // RESTRICT, the same defensive default EnrolmentConfiguration uses for its own pupil
        // reference: no delete route exists for a pupil once an admission record exists.
        builder.HasOne<Pupil>()
            .WithMany()
            .HasForeignKey(record => record.PupilId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AcademicSession>()
            .WithMany()
            .HasForeignKey(record => record.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ClassLevel>()
            .WithMany()
            .HasForeignKey(record => record.ClassAdmittedInto)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
