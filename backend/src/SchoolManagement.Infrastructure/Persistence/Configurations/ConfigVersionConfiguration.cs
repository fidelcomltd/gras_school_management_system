using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for the append-only <see cref="ConfigVersion"/> ledger (spec 6.2.9; TASK-0005a).</summary>
internal sealed class ConfigVersionConfiguration : IEntityTypeConfiguration<ConfigVersion>
{
    /// <summary>An account id's string form comfortably fits; matches <c>IdempotencyRecordConfiguration.CallerMaxLength</c>.</summary>
    private const int ActorAdminIdMaxLength = 128;

    private const int ChangedGroupMaxLength = 40;

    /// <summary>Generous headroom over any reason a future group requires (TASK-0005c's abbreviation reason included).</summary>
    private const int ReasonMaxLength = 1000;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ConfigVersion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("config_versions");

        builder.HasKey(version => version.Id);

        builder.Property(version => version.Id)
            .ValueGeneratedNever();

        // Real PostgreSQL `GENERATED ALWAYS AS IDENTITY` — see the entity's remarks for why this is a
        // database sequence rather than an application-computed "read max, add one", which would race
        // under concurrent saves. `Always` (not `ByDefault`) means the database rejects an explicit
        // value from application code outright; nothing in this codebase ever supplies one.
        builder.Property(version => version.VersionNumber)
            .IsRequired()
            .ValueGeneratedOnAdd()
            .UseIdentityAlwaysColumn();

        builder.HasIndex(version => version.VersionNumber)
            .IsUnique()
            .HasDatabaseName("ix_config_versions_version_number_unique");

        builder.Property(version => version.SnapshotJson)
            .IsRequired()
            .HasColumnName("snapshot")
            .HasColumnType("jsonb");

        builder.Property(version => version.ChangedGroup)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(ChangedGroupMaxLength);

        builder.Property(version => version.ActorAdminId)
            .HasMaxLength(ActorAdminIdMaxLength);

        builder.Property(version => version.Reason)
            .HasMaxLength(ReasonMaxLength);

        builder.Property(version => version.CreatedAtUtc)
            .IsRequired();
    }
}
