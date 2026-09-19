using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="TraitRating"/> (spec 09 §6.7.3; TASK-0083 stage 1).</summary>
internal sealed class TraitRatingConfiguration : IEntityTypeConfiguration<TraitRating>
{
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TraitRating> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("trait_rating");

        builder.HasKey(rating => rating.Id);
        builder.Property(rating => rating.Id).ValueGeneratedNever();

        builder.Property(rating => rating.ResultSetId).IsRequired();
        builder.Property(rating => rating.PupilId).IsRequired();
        builder.Property(rating => rating.TraitId).IsRequired();
        builder.Property(rating => rating.RatingScalePointId).IsRequired();

        builder.Property(rating => rating.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(rating => rating.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // One rating per pupil per trait per result set (spec: "One row per pupil per trait per
        // term") — no void concept here, unlike subject_score, so a plain unique index suffices.
        builder.HasIndex(rating => new { rating.ResultSetId, rating.PupilId, rating.TraitId })
            .IsUnique()
            .HasDatabaseName("ix_trait_rating_result_set_pupil_trait_unique");

        builder.HasIndex(rating => rating.TraitId)
            .HasDatabaseName("ix_trait_rating_trait_id");

        // RESTRICT, same defensive default the rest of this module uses.
        builder.HasOne<ResultSet>()
            .WithMany()
            .HasForeignKey(rating => rating.ResultSetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Pupils.Pupil>()
            .WithMany()
            .HasForeignKey(rating => rating.PupilId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Trait>()
            .WithMany()
            .HasForeignKey(rating => rating.TraitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RatingScalePoint>()
            .WithMany()
            .HasForeignKey(rating => rating.RatingScalePointId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
