using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="GradingBand"/> (spec 6.2.5; seed replaced by amendment 6.2.13; TASK-0069).</summary>
internal sealed class GradingBandConfiguration : IEntityTypeConfiguration<GradingBand>
{
    // Fixed, documented seed ids — same convention as SeededClassLevels. Never regenerate.
    private static readonly Guid APlusId = new("00000000-0000-0000-0000-000000000401");
    private static readonly Guid AId = new("00000000-0000-0000-0000-000000000402");
    private static readonly Guid BId = new("00000000-0000-0000-0000-000000000403");
    private static readonly Guid BMinusId = new("00000000-0000-0000-0000-000000000404");
    private static readonly Guid CPlusId = new("00000000-0000-0000-0000-000000000405");
    private static readonly Guid CId = new("00000000-0000-0000-0000-000000000406");
    private static readonly Guid DId = new("00000000-0000-0000-0000-000000000407");
    private static readonly Guid EId = new("00000000-0000-0000-0000-000000000408");
    private static readonly Guid FId = new("00000000-0000-0000-0000-000000000409");

    /// <summary>
    /// The same fixed ids as <see cref="SeedBands"/> inserts, in the same order as
    /// <see cref="GradingScaleSeed.SeededBands"/> (A+ through F) — <c>internal</c>, not private, so
    /// <c>ApiTestFixture.ResetDatabaseAsync</c> can reinsert the MIGRATION'S OWN rows after a TRUNCATE
    /// (EF Core never re-applies <c>HasData</c> on its own), rather than re-deriving ids some other
    /// way and only proving that derivation equals itself.
    /// </summary>
    internal static readonly IReadOnlyList<Guid> SeededIds = [APlusId, AId, BId, BMinusId, CPlusId, CId, DId, EId, FId];

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GradingBand> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("grading_band");

        builder.HasKey(band => band.Id);
        builder.Property(band => band.Id).ValueGeneratedNever();

        builder.Property(band => band.LowerBound).IsRequired();
        builder.Property(band => band.UpperBound).IsRequired();

        builder.Property(band => band.GradeLetter)
            .IsRequired()
            .HasMaxLength(GradingBand.GradeLetterMaxLength);

        builder.Property(band => band.Remark)
            .IsRequired()
            .HasMaxLength(GradingBand.RemarkMaxLength);

        builder.Property(band => band.DisplayOrder).IsRequired();

        SeedBands(builder);
    }

    /// <summary>Spec 6.2.13: nine bands, replacing 6.2.5's superseded six-band seed.</summary>
    private static void SeedBands(EntityTypeBuilder<GradingBand> builder)
    {
        var seedRows = GradingScaleSeed.SeededBands
            .Select((band, index) => new
            {
                Id = SeededIds[index],
                band.LowerBound,
                band.UpperBound,
                band.GradeLetter,
                band.Remark,
                DisplayOrder = index + 1,
            });

        builder.HasData(seedRows);
    }
}
