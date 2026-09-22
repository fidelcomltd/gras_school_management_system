using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="ResultSetSnapshot"/>: append-only, one row per (result set, revision).</summary>
internal sealed class ResultSetSnapshotConfiguration : IEntityTypeConfiguration<ResultSetSnapshot>
{
    public void Configure(EntityTypeBuilder<ResultSetSnapshot> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("result_set_snapshot");
        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id).ValueGeneratedNever();
        builder.Property(snapshot => snapshot.SnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(snapshot => snapshot.PublishedAtUtc).IsRequired();

        builder.HasOne<ResultSet>()
            .WithMany()
            .HasForeignKey(snapshot => snapshot.ResultSetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(snapshot => new { snapshot.ResultSetId, snapshot.RevisionNumber })
            .IsUnique()
            .HasDatabaseName("ux_result_set_snapshot_result_set_id_revision_number");
    }
}
