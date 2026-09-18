using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="RatingScale"/> (spec 6.2.13; TASK-0072 stage 1). No EF navigation to <see cref="RatingScalePoint"/> — see <see cref="RatingScale"/>'s remarks.</summary>
internal sealed class RatingScaleConfiguration : IEntityTypeConfiguration<RatingScale>
{
    // Fixed, documented seed ids — same convention as GradingBandConfiguration. Never regenerate.
    private static readonly Guid NurseryDevelopmentId = new("00000000-0000-0000-0000-000000000601");
    private static readonly Guid PrimaryTraitId = new("00000000-0000-0000-0000-000000000602");
    private static readonly Guid FivePointNumericId = new("00000000-0000-0000-0000-000000000603");

    /// <summary>
    /// The same fixed ids as <see cref="SeedScales"/> inserts, in the same order as
    /// <see cref="RatingScaleSeed.SeededScales"/> — <c>internal</c>, not private, so
    /// <c>ApiTestFixture.ResetDatabaseAsync</c> can reinsert the MIGRATION'S OWN rows after a
    /// TRUNCATE, and so <see cref="RatingScalePointConfiguration"/> can reference the same parent ids.
    /// </summary>
    internal static readonly IReadOnlyList<Guid> SeededIds = [NurseryDevelopmentId, PrimaryTraitId, FivePointNumericId];

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RatingScale> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("rating_scale");

        builder.HasKey(scale => scale.Id);
        builder.Property(scale => scale.Id).ValueGeneratedNever();

        builder.Property(scale => scale.Name)
            .IsRequired()
            .HasMaxLength(RatingScale.NameMaxLength);

        // No Points navigation is mapped — RatingScalePointConfiguration owns the FK. EF Core would
        // otherwise try (and fail) to discover a collection navigation from the read-only Points
        // property with no backing field convention behind it.
        builder.Ignore(scale => scale.Points);

        SeedScales(builder);
    }

    /// <summary>Spec 6.2.13: three seeded scales.</summary>
    private static void SeedScales(EntityTypeBuilder<RatingScale> builder)
    {
        var seedRows = RatingScaleSeed.SeededScales
            .Select((scale, index) => new { Id = SeededIds[index], scale.Name });

        builder.HasData(seedRows);
    }
}
