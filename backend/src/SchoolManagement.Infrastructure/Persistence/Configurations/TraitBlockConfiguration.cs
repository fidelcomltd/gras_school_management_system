using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="TraitBlock"/> (spec 6.2.13; TASK-0072 stage 3b) — exactly two rows, keyed by
/// <see cref="TraitDomain"/> rather than a generated id.
/// </summary>
internal sealed class TraitBlockConfiguration : IEntityTypeConfiguration<TraitBlock>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TraitBlock> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("trait_block");

        builder.HasKey(block => block.Id);
        builder.Property(block => block.Id)
            .HasConversion<string>()
            .HasMaxLength(20)
            .ValueGeneratedNever();

        builder.Property(block => block.RatingScaleId).IsRequired();

        // Restrict, not cascade: a rating scale's removal is already gated by IRatingScaleUsageGate
        // (extended by this stage to also count trait_block references) before it can vanish — this FK
        // is a second, database-level backstop, matching DevelopmentDomainConfiguration's own reasoning.
        builder.HasOne<RatingScale>()
            .WithMany()
            .HasForeignKey(block => block.RatingScaleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(block => block.RatingScaleId);

        SeedBlocks(builder);
    }

    /// <summary>
    /// Spec 6.2.13: "Both blocks point at the Primary trait scale" — the fixed seeded scale id
    /// <see cref="RatingScaleConfiguration.SeededIds"/>[1] (<see cref="RatingScaleSeed.PrimaryTraitName"/>).
    /// </summary>
    private static void SeedBlocks(EntityTypeBuilder<TraitBlock> builder)
    {
        var primaryTraitScaleId = RatingScaleConfiguration.SeededIds[1];

        builder.HasData(
            new { Id = TraitDomain.Affective, RatingScaleId = primaryTraitScaleId },
            new { Id = TraitDomain.Psychomotor, RatingScaleId = primaryTraitScaleId });
    }
}
