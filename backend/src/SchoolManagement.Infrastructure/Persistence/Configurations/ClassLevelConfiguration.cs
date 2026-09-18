using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Classes;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="ClassLevel"/> (spec 6.4.2, 6.4.9).</summary>
internal sealed class ClassLevelConfiguration : IEntityTypeConfiguration<ClassLevel>
{
    /// <summary>Maximum length of the stored audit-actor column, matching <c>RoleConfiguration</c>.</summary>
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ClassLevel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("class_levels");

        builder.HasKey(level => level.Id);
        builder.Property(level => level.Id).ValueGeneratedNever();

        builder.Property(level => level.Name)
            .IsRequired()
            .HasMaxLength(ClassLevel.NameMaxLength);

        builder.Property(level => level.NameKey)
            .IsRequired()
            .HasMaxLength(ClassLevel.NameMaxLength)
            .HasColumnName("name_key");

        builder.Property(level => level.SectionId).IsRequired();
        builder.Property(level => level.ProgressionOrder).IsRequired();
        builder.Property(level => level.NextLevelId);

        builder.Property(level => level.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(level => level.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(level => level.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Spec 6.4.2: "Unique, case-insensitive" — no status carve-out, same reasoning as
        // RoleConfiguration's own name_key index: an inactive level's name still blocks reuse.
        builder.HasIndex(level => level.NameKey)
            .IsUnique()
            .HasDatabaseName("ix_class_levels_name_key_unique");

        // Spec 6.4.2: "Unique across active levels" — a PARTIAL index, so two inactive levels (or an
        // active and an inactive one) may freely share a historical order value.
        builder.HasIndex(level => level.ProgressionOrder)
            .IsUnique()
            .HasFilter("status = 'Active'")
            .HasDatabaseName("ix_class_levels_progression_order_active_unique");

        // Self-referencing FK, RESTRICT: a backstop for DeleteLevelHandler's own
        // FindReferencingNextLevelAsync check (same "friendly check plus a DB backstop" shape
        // TermConfiguration's single-active-term index has for OpenTermHandler). No navigation
        // property either side — this entity never needs to load its own next level as an object.
        builder.HasOne<ClassLevel>()
            .WithMany()
            .HasForeignKey(level => level.NextLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        SeedSections(builder);
    }

    /// <summary>
    /// Spec 6.4.2: the nine seeded levels, chained Nursery 1 → ... → Primary 6. Seeded in REVERSE
    /// chain order (graduating level first) — <c>next_level_id</c> is a self-referencing FK, and
    /// PostgreSQL's per-row constraint check only sees rows already inserted EARLIER in the same
    /// migration, so a forward pointer (Nursery 1 → Nursery 2 inserted before Nursery 2 exists) would
    /// fail. <c>SeededClassLevels.Levels</c> itself stays in natural chain order for readability;
    /// only the seed statement's row order is reversed.
    /// </summary>
    private static void SeedSections(EntityTypeBuilder<ClassLevel> builder)
    {
        var seedRows = SeededClassLevels.Levels.Reverse().Select(level => new
        {
            level.Id,
            level.Name,
            NameKey = level.Name.ToLowerInvariant(),
            level.SectionId,
            level.ProgressionOrder,
            level.NextLevelId,
            Status = LevelStatus.Active,
            CreatedAtUtc = SeededClassLevels.SeedTimestamp,
            CreatedBy = (string?)null,
            ModifiedAtUtc = (DateTimeOffset?)null,
            ModifiedBy = (string?)null,
            level.Version,
        });

        builder.HasData(seedRows);
    }
}
