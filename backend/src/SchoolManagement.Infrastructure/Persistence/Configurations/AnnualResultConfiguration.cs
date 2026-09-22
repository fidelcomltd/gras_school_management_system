using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="AnnualResult"/>: one row per (session, pupil).</summary>
internal sealed class AnnualResultConfiguration : IEntityTypeConfiguration<AnnualResult>
{
    public void Configure(EntityTypeBuilder<AnnualResult> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("annual_result");
        builder.HasKey(result => result.Id);
        builder.Property(result => result.Id).ValueGeneratedNever();
        foreach (var average in new[] { nameof(AnnualResult.FirstTermAverage), nameof(AnnualResult.SecondTermAverage), nameof(AnnualResult.ThirdTermAverage), nameof(AnnualResult.CumulativeAverage) })
        {
            builder.Property(average).HasPrecision(5, 2);
        }

        builder.Property(result => result.CumulativeGrade).HasMaxLength(GradingBand.GradeLetterMaxLength).IsRequired();
        builder.Property(result => result.CumulativeRemark).HasMaxLength(GradingBand.RemarkMaxLength).IsRequired();
        builder.Property(result => result.SubjectsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(result => result.ProposedOutcome).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne<AcademicSession>().WithMany().HasForeignKey(result => result.SessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Arm>().WithMany().HasForeignKey(result => result.ArmId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Pupil>().WithMany().HasForeignKey(result => result.PupilId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(result => new { result.SessionId, result.PupilId })
            .IsUnique()
            .HasDatabaseName("ux_annual_result_session_id_pupil_id");
    }
}
