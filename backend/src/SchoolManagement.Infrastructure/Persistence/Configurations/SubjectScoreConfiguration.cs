using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="SubjectScore"/> (spec 09 §6.7.3; TASK-0076).</summary>
internal sealed class SubjectScoreConfiguration : IEntityTypeConfiguration<SubjectScore>
{
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SubjectScore> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("subject_score");

        builder.HasKey(score => score.Id);
        builder.Property(score => score.Id).ValueGeneratedNever();

        builder.Property(score => score.ResultSetId).IsRequired();
        builder.Property(score => score.PupilId).IsRequired();
        builder.Property(score => score.SubjectId).IsRequired();
        builder.Property(score => score.TermId).IsRequired();

        // Raw JSON text (jsonb) — a map from assessment component id to an integer mark. Opaque to
        // this entity and to the database on purpose (TASK-0069's "no literal component count" rule).
        builder.Property(score => score.ComponentMarksJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(score => score.ExamAbsent).IsRequired();

        builder.Property(score => score.VoidedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(score => score.VoidReason).HasMaxLength(SubjectScore.VoidReasonMaxLength);

        builder.Property(score => score.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(score => score.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // THE database enforcement of spec 6.7.3's uniqueness rule — a PARTIAL index, same shape as
        // SubjectMappingConfiguration's active-only index and EnrolmentConfiguration's one-open-row
        // invariant. Without the filter, a voided row would permanently block re-entry for the same
        // pupil/subject/term.
        builder.HasIndex(score => new { score.PupilId, score.SubjectId, score.TermId })
            .IsUnique()
            .HasFilter("voided_at IS NULL")
            .HasDatabaseName("ix_subject_score_pupil_subject_term_active_unique");

        builder.HasIndex(score => score.ResultSetId)
            .HasDatabaseName("ix_subject_score_result_set_id");

        builder.HasIndex(score => score.TermId)
            .HasDatabaseName("ix_subject_score_term_id");

        // RESTRICT, same defensive default the rest of this module uses — no delete route exists for
        // a result set, a pupil, a subject or a term once marks reference them.
        builder.HasOne<Domain.Results.ResultSet>()
            .WithMany()
            .HasForeignKey(score => score.ResultSetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Pupils.Pupil>()
            .WithMany()
            .HasForeignKey(score => score.PupilId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Subject>()
            .WithMany()
            .HasForeignKey(score => score.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Sessions.Term>()
            .WithMany()
            .HasForeignKey(score => score.TermId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
