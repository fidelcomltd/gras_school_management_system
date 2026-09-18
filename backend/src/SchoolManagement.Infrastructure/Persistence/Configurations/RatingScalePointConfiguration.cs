using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="RatingScalePoint"/> (spec 6.2.13; TASK-0072 stage 1).</summary>
internal sealed class RatingScalePointConfiguration : IEntityTypeConfiguration<RatingScalePoint>
{
    // Fixed, documented seed ids, one per point across all three seeded scales in
    // RatingScaleSeed.SeededScales order (Nursery development's 4, then Primary trait's 3, then
    // Five-point numeric's 5) — same convention as GradingBandConfiguration. Never regenerate.
    private static readonly Guid[] SeededIds =
    [
        new("00000000-0000-0000-0000-000000000701"),
        new("00000000-0000-0000-0000-000000000702"),
        new("00000000-0000-0000-0000-000000000703"),
        new("00000000-0000-0000-0000-000000000704"),
        new("00000000-0000-0000-0000-000000000705"),
        new("00000000-0000-0000-0000-000000000706"),
        new("00000000-0000-0000-0000-000000000707"),
        new("00000000-0000-0000-0000-000000000708"),
        new("00000000-0000-0000-0000-000000000709"),
        new("00000000-0000-0000-0000-000000000710"),
        new("00000000-0000-0000-0000-000000000711"),
        new("00000000-0000-0000-0000-000000000712"),
    ];

    /// <summary>
    /// <see cref="SeededIds"/>, exposed for <c>ApiTestFixture.ResetDatabaseAsync</c> to reinsert the
    /// MIGRATION'S OWN rows after a TRUNCATE — same convention as <see cref="RatingScaleConfiguration.SeededIds"/>.
    /// </summary>
    internal static IReadOnlyList<Guid> AllSeededIds => SeededIds;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RatingScalePoint> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("rating_scale_point");

        builder.HasKey(point => point.Id);
        builder.Property(point => point.Id).ValueGeneratedNever();

        builder.Property(point => point.RatingScaleId).IsRequired();

        builder.Property(point => point.PointCode)
            .IsRequired()
            .HasMaxLength(RatingScalePoint.PointCodeMaxLength);

        builder.Property(point => point.PointLabel)
            .IsRequired()
            .HasMaxLength(RatingScalePoint.PointLabelMaxLength);

        builder.Property(point => point.PointOrder).IsRequired();

        builder.HasOne<RatingScale>()
            .WithMany()
            .HasForeignKey(point => point.RatingScaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(point => point.RatingScaleId);

        SeedPoints(builder);
    }

    /// <summary>Spec 6.2.13: the three seeded scales' points, flattened in seed order.</summary>
    private static void SeedPoints(EntityTypeBuilder<RatingScalePoint> builder)
    {
        var seedRows = new List<object>();
        var idIndex = 0;

        for (var scaleIndex = 0; scaleIndex < RatingScaleSeed.SeededScales.Count; scaleIndex++)
        {
            var scale = RatingScaleSeed.SeededScales[scaleIndex];
            var scaleId = RatingScaleConfiguration.SeededIds[scaleIndex];

            foreach (var point in scale.Points)
            {
                seedRows.Add(new
                {
                    Id = SeededIds[idIndex],
                    RatingScaleId = scaleId,
                    point.PointCode,
                    point.PointLabel,
                    point.PointOrder,
                });
                idIndex++;
            }
        }

        builder.HasData(seedRows);
    }
}
