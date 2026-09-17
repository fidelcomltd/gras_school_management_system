using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="SubjectMappingException"/> (spec 6.6.4; TASK-0070).</summary>
internal sealed class SubjectMappingExceptionConfiguration : IEntityTypeConfiguration<SubjectMappingException>
{
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SubjectMappingException> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("subject_mapping_exception");

        builder.HasKey(exception => exception.Id);
        builder.Property(exception => exception.Id).ValueGeneratedNever();

        builder.Property(exception => exception.ArmId).IsRequired();
        builder.Property(exception => exception.SubjectId).IsRequired();
        builder.Property(exception => exception.TermId).IsRequired();

        builder.Property(exception => exception.Mode)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(exception => exception.Reason)
            .IsRequired()
            .HasMaxLength(SubjectMappingException.ReasonMaxLength);

        builder.Property(exception => exception.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(exception => exception.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Confirmed as proposed (TASK-0070 delta): active-uniqueness on (arm_id, subject_id, term_id)
        // REGARDLESS OF MODE — this row has no status column (it is deleted, never "ended"), so a
        // single ordinary unique index (not partial) is the whole rule: an include and an exclude on
        // the same triple can never coexist, and the same-mode duplicate case is covered for free.
        builder.HasIndex(exception => new { exception.ArmId, exception.SubjectId, exception.TermId })
            .IsUnique()
            .HasDatabaseName("ix_subject_mapping_exception_arm_subject_term_unique");

        builder.HasIndex(exception => exception.TermId)
            .HasDatabaseName("ix_subject_mapping_exception_term_id");

        builder.HasOne<Domain.Classes.Arm>()
            .WithMany()
            .HasForeignKey(exception => exception.ArmId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Subject>()
            .WithMany()
            .HasForeignKey(exception => exception.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Sessions.Term>()
            .WithMany()
            .HasForeignKey(exception => exception.TermId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
