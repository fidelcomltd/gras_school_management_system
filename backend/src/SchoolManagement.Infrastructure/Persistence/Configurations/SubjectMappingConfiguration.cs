using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="SubjectMapping"/> (spec 6.6.3; TASK-0070). NO rows are seeded — see the card's AC-2.</summary>
internal sealed class SubjectMappingConfiguration : IEntityTypeConfiguration<SubjectMapping>
{
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SubjectMapping> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("subject_mapping");

        builder.HasKey(mapping => mapping.Id);
        builder.Property(mapping => mapping.Id).ValueGeneratedNever();

        builder.Property(mapping => mapping.SubjectId).IsRequired();
        builder.Property(mapping => mapping.ClassLevelId).IsRequired();
        builder.Property(mapping => mapping.SessionId).IsRequired();
        builder.Property(mapping => mapping.TermId).IsRequired();
        builder.Property(mapping => mapping.DisplayOrder).IsRequired();

        builder.Property(mapping => mapping.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(mapping => mapping.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(mapping => mapping.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Spec 6.6.3: "Unique on (subject_id, class_level_id, term_id) where status is active" — a
        // PARTIAL index, same shape as ClassLevelConfiguration's progression_order index and the
        // one-open-row invariant on enrolment (TASK-0059): two ENDED mappings, or an ended and an
        // active one under different terms, may freely share the triple.
        builder.HasIndex(mapping => new { mapping.SubjectId, mapping.ClassLevelId, mapping.TermId })
            .IsUnique()
            .HasFilter("status = 'Active'")
            .HasDatabaseName("ix_subject_mapping_subject_level_term_active_unique");

        builder.HasIndex(mapping => mapping.TermId)
            .HasDatabaseName("ix_subject_mapping_term_id");

        // RESTRICT: DeleteSubjectHandler's own friendly check runs first; this is the database
        // backstop, same shape as ArmConfiguration's FK to ClassLevel.
        builder.HasOne<Subject>()
            .WithMany()
            .HasForeignKey(mapping => mapping.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Classes.ClassLevel>()
            .WithMany()
            .HasForeignKey(mapping => mapping.ClassLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Sessions.AcademicSession>()
            .WithMany()
            .HasForeignKey(mapping => mapping.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Sessions.Term>()
            .WithMany()
            .HasForeignKey(mapping => mapping.TermId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
