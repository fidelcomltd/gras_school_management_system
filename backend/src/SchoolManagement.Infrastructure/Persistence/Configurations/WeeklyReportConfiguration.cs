using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Weekly;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="WeeklyReport"/> (spec 6.10.5).</summary>
internal sealed class WeeklyReportConfiguration : IEntityTypeConfiguration<WeeklyReport>
{
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WeeklyReport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("weekly_report", table => table.HasCheckConstraint(
            "ck_weekly_report_week_number", $"week_number BETWEEN 1 AND {TermWeeks.MaxWeeks}"));

        builder.HasKey(report => report.Id);
        builder.Property(report => report.Id).ValueGeneratedNever();
        builder.Property(report => report.State).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(report => report.PublishedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(report => report.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(report => report.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        builder.HasIndex(report => new { report.PupilId, report.TermId, report.WeekNumber })
            .IsUnique()
            .HasDatabaseName("ix_weekly_report_pupil_term_week_unique");
        builder.HasIndex(report => new { report.ArmId, report.TermId, report.WeekNumber })
            .HasDatabaseName("ix_weekly_report_arm_term_week");

        builder.HasMany(report => report.Days)
            .WithOne()
            .HasForeignKey(day => day.WeeklyReportId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(report => report.Days).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Domain.Pupils.Pupil>().WithMany().HasForeignKey(report => report.PupilId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Classes.Arm>().WithMany().HasForeignKey(report => report.ArmId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Sessions.Term>().WithMany().HasForeignKey(report => report.TermId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for <see cref="WeeklyReportDay"/> (spec 6.10.6).</summary>
internal sealed class WeeklyReportDayConfiguration : IEntityTypeConfiguration<WeeklyReportDay>
{
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WeeklyReportDay> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("weekly_report_day");
        builder.HasKey(day => day.Id);
        builder.Property(day => day.Id).ValueGeneratedNever();
        builder.Property(day => day.DayOfWeek).IsRequired().HasConversion<string>().HasMaxLength(16);

        builder.Property(day => day.Behaviour).HasMaxLength(WeeklyReportDay.LineMaxLength);
        builder.Property(day => day.Performance).HasMaxLength(WeeklyReportDay.LineMaxLength);
        builder.Property(day => day.Dressing).HasMaxLength(WeeklyReportDay.LineMaxLength);
        builder.Property(day => day.HomeWork).HasMaxLength(WeeklyReportDay.LineMaxLength);
        builder.Property(day => day.Eating).HasMaxLength(WeeklyReportDay.LineMaxLength);
        builder.Property(day => day.SymptomsOfIllness).HasMaxLength(WeeklyReportDay.LineMaxLength);
        builder.Property(day => day.TeacherComment).HasMaxLength(WeeklyReportDay.CommentMaxLength);
        builder.Property(day => day.ParentComment).HasMaxLength(WeeklyReportDay.CommentMaxLength);
        builder.Property(day => day.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(day => day.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        builder.HasIndex(day => new { day.WeeklyReportId, day.DayOfWeek })
            .IsUnique()
            .HasDatabaseName("ix_weekly_report_day_report_day_unique");
    }
}

/// <summary>Mapping for <see cref="ArmWeeklySetting"/> (spec 6.10.8).</summary>
internal sealed class ArmWeeklySettingConfiguration : IEntityTypeConfiguration<ArmWeeklySetting>
{
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ArmWeeklySetting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("arm_weekly_setting");
        builder.HasKey(setting => setting.ArmId);
        builder.Property(setting => setting.ArmId).ValueGeneratedNever();
        builder.Property(setting => setting.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(setting => setting.ModifiedBy).HasMaxLength(AuditActorMaxLength);
        builder.HasOne<Domain.Classes.Arm>().WithMany().HasForeignKey(setting => setting.ArmId).OnDelete(DeleteBehavior.Cascade);
    }
}
