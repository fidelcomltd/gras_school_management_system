using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="PupilRemark"/> (spec 09 §6.7.3; appendix C.6; TASK-0086 stage A).</summary>
internal sealed class PupilRemarkConfiguration : IEntityTypeConfiguration<PupilRemark>
{
    private const int AuditActorMaxLength = 128;
    private const int StaffNameSnapshotMaxLength = 200;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PupilRemark> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("pupil_remark");

        builder.HasKey(remark => remark.Id);
        builder.Property(remark => remark.Id).ValueGeneratedNever();

        builder.Property(remark => remark.ResultSetId).IsRequired();
        builder.Property(remark => remark.PupilId).IsRequired();
        builder.Property(remark => remark.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(remark => remark.Text).HasMaxLength(PupilRemark.TextMaxLength).IsRequired();
        builder.Property(remark => remark.WrittenByAdminId).IsRequired();
        builder.Property(remark => remark.WrittenByName).HasMaxLength(StaffNameSnapshotMaxLength).IsRequired();
        builder.Property(remark => remark.WrittenAtUtc).IsRequired();

        builder.Property(remark => remark.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(remark => remark.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // One remark of each kind per pupil per result set.
        builder.HasIndex(remark => new { remark.ResultSetId, remark.PupilId, remark.Kind })
            .IsUnique()
            .HasDatabaseName("ix_pupil_remark_result_set_pupil_kind_unique");

        // RESTRICT, same defensive default the rest of this module uses. The writer's account is
        // referenced but never cascaded — a remark survives the account that wrote it, the same
        // posture spec 6.1.12's audit trail takes.
        builder.HasOne<ResultSet>()
            .WithMany()
            .HasForeignKey(remark => remark.ResultSetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Pupils.Pupil>()
            .WithMany()
            .HasForeignKey(remark => remark.PupilId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Auth.AdminAccount>()
            .WithMany()
            .HasForeignKey(remark => remark.WrittenByAdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
