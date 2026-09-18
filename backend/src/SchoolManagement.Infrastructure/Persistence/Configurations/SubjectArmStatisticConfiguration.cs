using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="SubjectArmStatistic"/> (spec 09 §6.7.6; TASK-0071).</summary>
internal sealed class SubjectArmStatisticConfiguration : IEntityTypeConfiguration<SubjectArmStatistic>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SubjectArmStatistic> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("subject_arm_statistic");

        builder.HasKey(statistic => statistic.Id);
        builder.Property(statistic => statistic.Id).ValueGeneratedNever();

        builder.Property(statistic => statistic.ResultSetId).IsRequired();
        builder.Property(statistic => statistic.SubjectId).IsRequired();
        builder.Property(statistic => statistic.ClassAverage).HasPrecision(5, 1);
        builder.Property(statistic => statistic.CountedPupils).IsRequired();
        builder.Property(statistic => statistic.RankedPupils).IsRequired();

        builder.HasIndex(statistic => statistic.ResultSetId)
            .HasDatabaseName("ix_subject_arm_statistic_result_set_id");

        // One statistic row per subject per result set (spec 6.7.6: "one per subject in effect").
        builder.HasIndex(statistic => new { statistic.ResultSetId, statistic.SubjectId })
            .IsUnique()
            .HasDatabaseName("ix_subject_arm_statistic_result_set_id_subject_id_unique");

        builder.HasOne<Domain.Results.ResultSet>()
            .WithMany()
            .HasForeignKey(statistic => statistic.ResultSetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Subjects.Subject>()
            .WithMany()
            .HasForeignKey(statistic => statistic.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
