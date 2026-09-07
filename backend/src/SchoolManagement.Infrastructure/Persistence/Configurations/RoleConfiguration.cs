using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="Role"/> (spec 6.1.4).</summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <summary>Maximum length of the stored audit-actor column, matching <c>AdminAccountConfiguration</c>.</summary>
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("roles");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Id)
            .ValueGeneratedNever();

        builder.Property(role => role.Name)
            .IsRequired()
            .HasMaxLength(Role.NameMaxLength);

        // A persistence detail (see the entity's own remarks): lets uniqueness be a plain unique
        // index rather than a functional one, and doubles as the sortable/keyset column for the
        // `sort=name` list order (RoleRepository.ListAsync) since C# has no relational </>  operator
        // to keyset-compare against `Name` case-insensitively in raw SQL either way.
        builder.Property(role => role.NameKey)
            .IsRequired()
            .HasMaxLength(Role.NameMaxLength)
            .HasColumnName("name_key");

        builder.Property(role => role.Description)
            .HasMaxLength(Role.DescriptionMaxLength);

        builder.Property(role => role.IsSystem).IsRequired();

        builder.Property(role => role.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // Comma-joined text, same technique and same reasoning as
        // AdminAccountConfiguration.PasswordHistoryHashes: every privilege code is a fixed,
        // dot-separated, comma-free vocabulary (PrivilegeRegistry), so the join is unambiguous, and a
        // plain text column needs no array value-conversion support from the provider.
        // PropertyAccessMode.Field is required because Privileges has no public setter for EF to use.
        var privilegesProperty = builder
            .Property(role => role.Privileges)
            .HasField("_privileges")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("privileges")
            .HasColumnType("text")
            .HasConversion(
                privileges => string.Join(',', privileges),
                text => string.IsNullOrEmpty(text)
                    ? new List<string>()
                    : text.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

        privilegesProperty.Metadata.SetValueComparer(
            new ValueComparer<IReadOnlyList<string>>(
                (left, right) => (left ?? Array.Empty<string>()).SequenceEqual(right ?? Array.Empty<string>()),
                value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode(StringComparison.Ordinal))),
                value => value.ToList()));

        builder.Property(role => role.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(role => role.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Spec 6.1.4: "Unique, case-insensitive" — no status carve-out (unlike admin email), an
        // archived role's name still blocks reuse.
        builder.HasIndex(role => role.NameKey)
            .IsUnique()
            .HasDatabaseName("ix_roles_name_key_unique");
    }
}
