using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Auth;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="AdminSession"/> (spec 6.1.11, spec 9.1).</summary>
internal sealed class AdminSessionConfiguration : IEntityTypeConfiguration<AdminSession>
{
    /// <summary>Hex-encoded SHA-256 digest length.</summary>
    private const int TokenHashLength = 64;

    /// <summary>Matches <see cref="AdminSessionRevocationReasons"/>'s longest constant, with headroom.</summary>
    private const int RevokedReasonMaxLength = 40;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AdminSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("admin_sessions");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id)
            .ValueGeneratedNever();

        builder.Property(session => session.AdminAccountId)
            .IsRequired();

        builder.Property(session => session.TokenHash)
            .IsRequired()
            .HasMaxLength(TokenHashLength)
            .IsFixedLength();

        builder.Property(session => session.CreatedAtUtc).IsRequired();
        builder.Property(session => session.IdleExpiresAtUtc).IsRequired();
        builder.Property(session => session.AbsoluteExpiresAtUtc).IsRequired();

        builder.Property(session => session.RevokedReason)
            .HasMaxLength(RevokedReasonMaxLength);

        // Every authenticated request looks a session up by its token hash — this is the one lookup
        // that runs on the hot path of the whole API surface.
        builder.HasIndex(session => session.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_admin_sessions_token_hash_unique");

        // Supports both the sign-in eviction query (oldest-first among active sessions for an
        // account) and the password-change revoke-all-others query.
        builder.HasIndex(session => new { session.AdminAccountId, session.CreatedAtUtc })
            .HasDatabaseName("ix_admin_sessions_account_created_at_utc");
    }
}
