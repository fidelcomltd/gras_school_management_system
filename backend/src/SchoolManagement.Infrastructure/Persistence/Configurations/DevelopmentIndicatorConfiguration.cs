using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="DevelopmentIndicator"/> (spec 6.2.13; TASK-0072 stage 2a).</summary>
internal sealed class DevelopmentIndicatorConfiguration : IEntityTypeConfiguration<DevelopmentIndicator>
{
    // Fixed, documented seed ids, one per indicator across all four seeded domains in
    // DevelopmentDomainSeed.NurseryDomains order (4, then 14, then 15, then 12 — 45 total) — same
    // convention as RatingScalePointConfiguration. Never regenerate.
    private static readonly Guid[] SeededIds =
    [
        new("00000000-0000-0000-0000-000000000901"),
        new("00000000-0000-0000-0000-000000000902"),
        new("00000000-0000-0000-0000-000000000903"),
        new("00000000-0000-0000-0000-000000000904"),
        new("00000000-0000-0000-0000-000000000905"),
        new("00000000-0000-0000-0000-000000000906"),
        new("00000000-0000-0000-0000-000000000907"),
        new("00000000-0000-0000-0000-000000000908"),
        new("00000000-0000-0000-0000-000000000909"),
        new("00000000-0000-0000-0000-000000000910"),
        new("00000000-0000-0000-0000-000000000911"),
        new("00000000-0000-0000-0000-000000000912"),
        new("00000000-0000-0000-0000-000000000913"),
        new("00000000-0000-0000-0000-000000000914"),
        new("00000000-0000-0000-0000-000000000915"),
        new("00000000-0000-0000-0000-000000000916"),
        new("00000000-0000-0000-0000-000000000917"),
        new("00000000-0000-0000-0000-000000000918"),
        new("00000000-0000-0000-0000-000000000919"),
        new("00000000-0000-0000-0000-000000000920"),
        new("00000000-0000-0000-0000-000000000921"),
        new("00000000-0000-0000-0000-000000000922"),
        new("00000000-0000-0000-0000-000000000923"),
        new("00000000-0000-0000-0000-000000000924"),
        new("00000000-0000-0000-0000-000000000925"),
        new("00000000-0000-0000-0000-000000000926"),
        new("00000000-0000-0000-0000-000000000927"),
        new("00000000-0000-0000-0000-000000000928"),
        new("00000000-0000-0000-0000-000000000929"),
        new("00000000-0000-0000-0000-000000000930"),
        new("00000000-0000-0000-0000-000000000931"),
        new("00000000-0000-0000-0000-000000000932"),
        new("00000000-0000-0000-0000-000000000933"),
        new("00000000-0000-0000-0000-000000000934"),
        new("00000000-0000-0000-0000-000000000935"),
        new("00000000-0000-0000-0000-000000000936"),
        new("00000000-0000-0000-0000-000000000937"),
        new("00000000-0000-0000-0000-000000000938"),
        new("00000000-0000-0000-0000-000000000939"),
        new("00000000-0000-0000-0000-000000000940"),
        new("00000000-0000-0000-0000-000000000941"),
        new("00000000-0000-0000-0000-000000000942"),
        new("00000000-0000-0000-0000-000000000943"),
        new("00000000-0000-0000-0000-000000000944"),
        new("00000000-0000-0000-0000-000000000945"),
    ];

    /// <summary>
    /// <see cref="SeededIds"/>, exposed for <c>ApiTestFixture.ResetDatabaseAsync</c> to reinsert the
    /// MIGRATION'S OWN rows after a TRUNCATE — same convention as
    /// <see cref="RatingScalePointConfiguration.AllSeededIds"/>.
    /// </summary>
    internal static IReadOnlyList<Guid> AllSeededIds => SeededIds;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DevelopmentIndicator> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("development_indicator");

        builder.HasKey(indicator => indicator.Id);
        builder.Property(indicator => indicator.Id).ValueGeneratedNever();

        builder.Property(indicator => indicator.DomainId).IsRequired();

        builder.Property(indicator => indicator.Name)
            .IsRequired()
            .HasMaxLength(DevelopmentIndicator.NameMaxLength);

        builder.Property(indicator => indicator.DisplayOrder).IsRequired();

        builder.Property(indicator => indicator.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne<DevelopmentDomain>()
            .WithMany()
            .HasForeignKey(indicator => indicator.DomainId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(indicator => indicator.DomainId);

        SeedIndicators(builder);
    }

    /// <summary>Spec 6.2.13 / Appendix E.3: the four seeded domains' indicators, flattened in seed order.</summary>
    private static void SeedIndicators(EntityTypeBuilder<DevelopmentIndicator> builder)
    {
        var seedRows = new List<object>();
        var idIndex = 0;

        for (var domainIndex = 0; domainIndex < DevelopmentDomainSeed.NurseryDomains.Count; domainIndex++)
        {
            var domain = DevelopmentDomainSeed.NurseryDomains[domainIndex];
            var domainId = DevelopmentDomainConfiguration.SeededIds[domainIndex];

            foreach (var indicator in domain.Indicators)
            {
                seedRows.Add(new
                {
                    Id = SeededIds[idIndex],
                    DomainId = domainId,
                    indicator.Name,
                    indicator.DisplayOrder,
                    Status = DevelopmentIndicatorStatus.Active,
                });
                idIndex++;
            }
        }

        builder.HasData(seedRows);
    }
}
