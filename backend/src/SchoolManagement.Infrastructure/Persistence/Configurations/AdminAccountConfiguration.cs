using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Auth;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="AdminAccount"/> (spec 6.1.3).</summary>
internal sealed class AdminAccountConfiguration : IEntityTypeConfiguration<AdminAccount>
{
    /// <summary>Maximum length of the stored audit-actor column, matching <c>SampleRecordConfiguration</c>.</summary>
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AdminAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("admin_accounts");

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Id)
            .ValueGeneratedNever();

        builder.Property(account => account.Email)
            .IsRequired()
            .HasMaxLength(AuthPolicy.EmailMaxLength);

        builder.Property(account => account.StaffName)
            .IsRequired()
            .HasMaxLength(AuthPolicy.StaffNameMaxLength);

        builder.Property(account => account.PasswordHash)
            .IsRequired()
            .HasMaxLength(AuthPolicy.PasswordHashMaxLength);

        // Spec 6.1.11: "the last five password hashes are retained per account." Stored as one
        // newline-joined text column rather than a native Postgres array: the encoded Argon2id hash
        // format never contains a newline (it is '$'-delimited base64), so the join is unambiguous,
        // and a plain-text column needs no array value-conversion support from the provider.
        // Read-only property backed by the private `_passwordHistoryHashes` field — PropertyAccessMode
        // .Field is required because the property has no setter for EF to use.
        var passwordHistoryProperty = builder
            .Property(account => account.PasswordHistoryHashes)
            .HasField("_passwordHistoryHashes")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("password_history_hashes")
            .HasColumnType("text")
            .HasConversion(
                hashes => string.Join('\n', hashes),
                text => string.IsNullOrEmpty(text)
                    ? new List<string>()
                    : text.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList());

        passwordHistoryProperty.Metadata.SetValueComparer(
            new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyList<string>>(
                (left, right) => (left ?? Array.Empty<string>()).SequenceEqual(right ?? Array.Empty<string>()),
                value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode(StringComparison.Ordinal))),
                value => value.ToList()));

        builder.Property(account => account.MustChangePassword).IsRequired();
        builder.Property(account => account.IsSuperAdmin).IsRequired();

        builder.Property(account => account.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(account => account.FailedLoginCount).IsRequired();

        builder.Property(account => account.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(account => account.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Active/suspended uniqueness only (spec 6.1.13: a deactivated account's email may be reused).
        // TASK-0019 owns the endpoints that ever create a SECOND row, but the constraint belongs on
        // the table from the start rather than being retrofitted once it matters.
        builder.HasIndex(account => account.Email)
            .IsUnique()
            .HasFilter("status <> 'Deactivated'")
            .HasDatabaseName("ix_admin_accounts_email_unique_not_deactivated");
    }
}
