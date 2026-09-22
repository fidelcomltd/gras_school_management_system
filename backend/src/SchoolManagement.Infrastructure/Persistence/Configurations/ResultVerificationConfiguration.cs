using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="ResultVerification"/>: one row per (result set, pupil, revision), unique token.</summary>
internal sealed class ResultVerificationConfiguration : IEntityTypeConfiguration<ResultVerification>
{
    public void Configure(EntityTypeBuilder<ResultVerification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("result_verification");
        builder.HasKey(verification => verification.Id);
        builder.Property(verification => verification.Id).ValueGeneratedNever();
        builder.Property(verification => verification.Token).HasMaxLength(ResultVerification.TokenLength).IsRequired();
        builder.Property(verification => verification.IssuedAtUtc).IsRequired();

        builder.HasOne<ResultSet>()
            .WithMany()
            .HasForeignKey(verification => verification.ResultSetId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Pupil>()
            .WithMany()
            .HasForeignKey(verification => verification.PupilId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(verification => verification.Token)
            .IsUnique()
            .HasDatabaseName("ux_result_verification_token");
        builder.HasIndex(verification => new { verification.ResultSetId, verification.PupilId, verification.RevisionNumber })
            .IsUnique()
            .HasDatabaseName("ux_result_verification_result_set_id_pupil_id_revision_number");
    }
}
