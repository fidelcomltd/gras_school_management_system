using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="DevelopmentDomain"/> (spec 6.2.13; TASK-0072 stage 2a). No EF navigation to <see cref="DevelopmentIndicator"/> — see <see cref="DevelopmentDomain"/>'s remarks.</summary>
internal sealed class DevelopmentDomainConfiguration : IEntityTypeConfiguration<DevelopmentDomain>
{
    // Fixed, documented seed ids, one per domain in DevelopmentDomainSeed.NurseryDomains order —
    // same convention as RatingScaleConfiguration. Never regenerate.
    private static readonly Guid MathsReadinessId = new("00000000-0000-0000-0000-000000000801");
    private static readonly Guid LanguageCommunicationId = new("00000000-0000-0000-0000-000000000802");
    private static readonly Guid PersonalPhysicalId = new("00000000-0000-0000-0000-000000000803");
    private static readonly Guid SocialEmotionalId = new("00000000-0000-0000-0000-000000000804");

    /// <summary>
    /// The same fixed ids as <see cref="SeedDomains"/> inserts, in the same order as
    /// <see cref="DevelopmentDomainSeed.NurseryDomains"/> — <c>internal</c>, not private, so
    /// <c>ApiTestFixture.ResetDatabaseAsync</c> can reinsert the MIGRATION'S OWN rows after a
    /// TRUNCATE, and so <see cref="DevelopmentIndicatorConfiguration"/> can reference the same parent ids.
    /// </summary>
    internal static readonly IReadOnlyList<Guid> SeededIds =
        [MathsReadinessId, LanguageCommunicationId, PersonalPhysicalId, SocialEmotionalId];

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DevelopmentDomain> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("development_domain");

        builder.HasKey(domain => domain.Id);
        builder.Property(domain => domain.Id).ValueGeneratedNever();

        builder.Property(domain => domain.SectionId).IsRequired();

        builder.Property(domain => domain.Name)
            .IsRequired()
            .HasMaxLength(DevelopmentDomain.NameMaxLength);

        builder.Property(domain => domain.DisplayOrder).IsRequired();

        builder.Property(domain => domain.RatingScaleId).IsRequired();

        builder.Property(domain => domain.AllowsIndicatorComment).IsRequired();

        builder.Property(domain => domain.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // No Indicators navigation is mapped — DevelopmentIndicatorConfiguration owns the FK. Same
        // reasoning as RatingScaleConfiguration.Ignore(scale => scale.Points).
        builder.Ignore(domain => domain.Indicators);

        // Restrict, not cascade: a section has no delete route (Section's own remarks) and a rating
        // scale's removal is already gated by IRatingScaleUsageGate before it can vanish — this FK is
        // a second, database-level backstop against either disappearing out from under a domain.
        builder.HasOne<Section>()
            .WithMany()
            .HasForeignKey(domain => domain.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RatingScale>()
            .WithMany()
            .HasForeignKey(domain => domain.RatingScaleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(domain => domain.SectionId);
        builder.HasIndex(domain => domain.RatingScaleId);

        SeedDomains(builder);
    }

    /// <summary>Spec 6.2.13 / Appendix E.3: the four seeded nursery domains.</summary>
    private static void SeedDomains(EntityTypeBuilder<DevelopmentDomain> builder)
    {
        var seedRows = DevelopmentDomainSeed.NurseryDomains
            .Select((domain, index) => new
            {
                Id = SeededIds[index],
                SectionId = SeededClassLevels.NurserySectionId,
                domain.Name,
                domain.DisplayOrder,
                RatingScaleId = RatingScaleConfiguration.SeededIds[0], // "Nursery development" — RatingScaleSeed.SeededScales[0].
                domain.AllowsIndicatorComment,
                Status = DevelopmentDomainStatus.Active,
            });

        builder.HasData(seedRows);
    }
}
