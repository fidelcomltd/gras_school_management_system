using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="PupilTermResult"/> (spec 09 §6.7.6; TASK-0071).</summary>
internal sealed class PupilTermResultConfiguration : IEntityTypeConfiguration<PupilTermResult>
{
    private const int GradeMaxLength = 3;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PupilTermResult> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("pupil_term_result");

        builder.HasKey(result => result.Id);
        builder.Property(result => result.Id).ValueGeneratedNever();

        builder.Property(result => result.ResultSetId).IsRequired();
        builder.Property(result => result.PupilId).IsRequired();
        builder.Property(result => result.SubjectsTaken).IsRequired();
        builder.Property(result => result.TotalObtainable).IsRequired();
        builder.Property(result => result.TotalObtained).IsRequired();
        builder.Property(result => result.Average).IsRequired().HasPrecision(5, 2);
        builder.Property(result => result.OverallGrade).IsRequired().HasMaxLength(GradeMaxLength);
        builder.Property(result => result.ArmPositionTied).IsRequired();
        builder.Property(result => result.ArmPupilCount).IsRequired();
        builder.Property(result => result.LevelPositionTied).IsRequired();

        builder.HasIndex(result => result.ResultSetId)
            .HasDatabaseName("ix_pupil_term_result_result_set_id");

        // One row per pupil per result set (spec 6.7.6: "written for every pupil on the roster").
        builder.HasIndex(result => new { result.ResultSetId, result.PupilId })
            .IsUnique()
            .HasDatabaseName("ix_pupil_term_result_result_set_id_pupil_id_unique");

        builder.HasOne<Domain.Results.ResultSet>()
            .WithMany()
            .HasForeignKey(result => result.ResultSetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Pupils.Pupil>()
            .WithMany()
            .HasForeignKey(result => result.PupilId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
