using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="Trait"/> (spec 6.2.7 / 6.2.13; TASK-0072 stage 3b).</summary>
internal sealed class TraitConfiguration : IEntityTypeConfiguration<Trait>
{
    // Fixed, documented seed ids, affective (11) then psychomotor (8), in Appendix F.3's printed
    // order — same convention as DevelopmentIndicatorConfiguration. Never regenerate.
    private static readonly Guid[] SeededIds =
    [
        new("00000000-0000-0000-0000-000000001001"), // Conduct
        new("00000000-0000-0000-0000-000000001002"), // Punctuality
        new("00000000-0000-0000-0000-000000001003"), // Honesty
        new("00000000-0000-0000-0000-000000001004"), // Neatness
        new("00000000-0000-0000-0000-000000001005"), // Attitude
        new("00000000-0000-0000-0000-000000001006"), // Attentiveness
        new("00000000-0000-0000-0000-000000001007"), // Co-operation
        new("00000000-0000-0000-0000-000000001008"), // Skills
        new("00000000-0000-0000-0000-000000001009"), // Perseverance
        new("00000000-0000-0000-0000-000000001010"), // Obedient
        new("00000000-0000-0000-0000-000000001011"), // Fluency
        new("00000000-0000-0000-0000-000000001012"), // Sports
        new("00000000-0000-0000-0000-000000001013"), // Social activities
        new("00000000-0000-0000-0000-000000001014"), // Painting and drawing
        new("00000000-0000-0000-0000-000000001015"), // Hand writing
        new("00000000-0000-0000-0000-000000001016"), // Mathematical Skills
        new("00000000-0000-0000-0000-000000001017"), // Reasoning
        new("00000000-0000-0000-0000-000000001018"), // Health
        new("00000000-0000-0000-0000-000000001019"), // Creativity
    ];

    /// <summary>
    /// <see cref="SeededIds"/>, exposed for <c>ApiTestFixture.ResetDatabaseAsync</c> to reinsert the
    /// MIGRATION'S OWN rows after a TRUNCATE — same convention as
    /// <see cref="DevelopmentIndicatorConfiguration.AllSeededIds"/>.
    /// </summary>
    internal static IReadOnlyList<Guid> AllSeededIds => SeededIds;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Trait> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("trait");

        builder.HasKey(trait => trait.Id);
        builder.Property(trait => trait.Id).ValueGeneratedNever();

        builder.Property(trait => trait.Domain)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(trait => trait.Name)
            .IsRequired()
            .HasMaxLength(Trait.NameMaxLength);

        builder.Property(trait => trait.DisplayOrder).IsRequired();

        builder.Property(trait => trait.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        SeedTraits(builder);
    }

    /// <summary>Spec 6.2.7 / Appendix F.3: the 11 seeded affective and 8 seeded psychomotor traits.</summary>
    private static void SeedTraits(EntityTypeBuilder<Trait> builder)
    {
        var seedRows = TraitSeed.AffectiveTraits
            .Concat(TraitSeed.PsychomotorTraits)
            .Select((trait, index) => new
            {
                Id = SeededIds[index],
                trait.Domain,
                trait.Name,
                trait.DisplayOrder,
                Status = TraitStatus.Active,
            });

        builder.HasData(seedRows);
    }
}
