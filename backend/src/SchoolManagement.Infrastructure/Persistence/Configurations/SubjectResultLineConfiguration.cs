using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="SubjectResultLine"/> (spec 09 §6.7.6; TASK-0071).</summary>
internal sealed class SubjectResultLineConfiguration : IEntityTypeConfiguration<SubjectResultLine>
{
    private const int GradeMaxLength = 3;
    private const int RemarkMaxLength = 40;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SubjectResultLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("subject_result_line");

        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();

        builder.Property(line => line.ResultSetId).IsRequired();
        builder.Property(line => line.PupilId).IsRequired();
        builder.Property(line => line.SubjectId).IsRequired();
        builder.Property(line => line.CaTotal).IsRequired();
        builder.Property(line => line.SubjectTotal).IsRequired();
        builder.Property(line => line.Grade).IsRequired().HasMaxLength(GradeMaxLength);
        builder.Property(line => line.Remark).IsRequired().HasMaxLength(RemarkMaxLength);
        builder.Property(line => line.SubjectPositionTied).IsRequired();
        builder.Property(line => line.IsPass).IsRequired();

        // Spec 8.2 step 11: every existing line for a result set is deleted and rewritten as one
        // batch — this index is what makes that delete a single indexed scan.
        builder.HasIndex(line => line.ResultSetId)
            .HasDatabaseName("ix_subject_result_line_result_set_id");

        // The broadsheet/pupil-result reads (a later card) fetch by result set + subject, and by
        // result set + pupil — both directions indexed now rather than discovered slow later.
        builder.HasIndex(line => new { line.ResultSetId, line.SubjectId })
            .HasDatabaseName("ix_subject_result_line_result_set_id_subject_id");
        builder.HasIndex(line => new { line.ResultSetId, line.PupilId })
            .HasDatabaseName("ix_subject_result_line_result_set_id_pupil_id");

        // RESTRICT, same defensive default every other FK in this module uses — no route deletes a
        // result_set today, so this never fires in practice, but a future one should have to decide
        // what happens to derived rows explicitly rather than inherit a cascade silently.
        builder.HasOne<Domain.Results.ResultSet>()
            .WithMany()
            .HasForeignKey(line => line.ResultSetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Pupils.Pupil>()
            .WithMany()
            .HasForeignKey(line => line.PupilId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Subjects.Subject>()
            .WithMany()
            .HasForeignKey(line => line.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
