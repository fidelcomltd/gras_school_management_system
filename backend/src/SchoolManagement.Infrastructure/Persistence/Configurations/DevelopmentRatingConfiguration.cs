using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="DevelopmentRating"/> (spec 02 §5.1; TASK-0083 stage 2).</summary>
internal sealed class DevelopmentRatingConfiguration : IEntityTypeConfiguration<DevelopmentRating>
{
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DevelopmentRating> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("development_rating");

        builder.HasKey(rating => rating.Id);
        builder.Property(rating => rating.Id).ValueGeneratedNever();

        builder.Property(rating => rating.ResultSetId).IsRequired();
        builder.Property(rating => rating.PupilId).IsRequired();
        builder.Property(rating => rating.IndicatorId).IsRequired();
        builder.Property(rating => rating.RatingScalePointId).IsRequired();

        builder.Property(rating => rating.Comment).HasMaxLength(DevelopmentRating.CommentMaxLength);

        builder.Property(rating => rating.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(rating => rating.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // One rating per pupil per indicator per result set (spec 02 §5.1: "One row per indicator per
        // term") — no void concept here, unlike subject_score, so a plain unique index suffices.
        builder.HasIndex(rating => new { rating.ResultSetId, rating.PupilId, rating.IndicatorId })
            .IsUnique()
            .HasDatabaseName("ix_development_rating_result_set_pupil_indicator_unique");

        builder.HasIndex(rating => rating.IndicatorId)
            .HasDatabaseName("ix_development_rating_indicator_id");

        // RESTRICT, same defensive default trait_rating uses.
        builder.HasOne<ResultSet>()
            .WithMany()
            .HasForeignKey(rating => rating.ResultSetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Pupils.Pupil>()
            .WithMany()
            .HasForeignKey(rating => rating.PupilId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DevelopmentIndicator>()
            .WithMany()
            .HasForeignKey(rating => rating.IndicatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RatingScalePoint>()
            .WithMany()
            .HasForeignKey(rating => rating.RatingScalePointId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
