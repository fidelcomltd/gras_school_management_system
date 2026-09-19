using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Classes;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="Section"/> (spec 6.4.2, 6.4.9).</summary>
internal sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    /// <summary>Maximum length of the stored audit-actor column, matching <c>RoleConfiguration</c>.</summary>
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("sections");

        builder.HasKey(section => section.Id);
        builder.Property(section => section.Id).ValueGeneratedNever();

        builder.Property(section => section.Name)
            .IsRequired()
            .HasMaxLength(Section.NameMaxLength);

        builder.Property(section => section.NameKey)
            .IsRequired()
            .HasMaxLength(Section.NameMaxLength)
            .HasColumnName("name_key");

        builder.Property(section => section.RatesTraits)
            .IsRequired()
            .HasColumnName("rates_traits");

        builder.Property(section => section.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(section => section.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        builder.HasIndex(section => section.NameKey)
            .IsUnique()
            .HasDatabaseName("ix_sections_name_key_unique");

        SeedSections(builder);
    }

    /// <summary>Spec 6.4.2: "Seeded with Nursery and Primary."</summary>
    private static void SeedSections(EntityTypeBuilder<Section> builder)
    {
        var seedRows = SeededClassLevels.Sections.Select(section => new
        {
            section.Id,
            section.Name,
            NameKey = section.Name.ToLowerInvariant(),
            section.RatesTraits,
            CreatedAtUtc = SeededClassLevels.SeedTimestamp,
            CreatedBy = (string?)null,
            ModifiedAtUtc = (DateTimeOffset?)null,
            ModifiedBy = (string?)null,
            section.Version,
        });

        builder.HasData(seedRows);
    }
}
