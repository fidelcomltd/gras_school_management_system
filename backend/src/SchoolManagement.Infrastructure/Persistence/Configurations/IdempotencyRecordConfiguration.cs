using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Idempotency;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="IdempotencyRecord"/> (TASK-0019, §9.9).</summary>
internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    /// <summary>Hex-encoded SHA-256 digest length.</summary>
    private const int HashLength = 64;

    /// <summary>An account id's string form comfortably fits; headroom for the anonymous sentinel.</summary>
    private const int CallerMaxLength = 128;

    private const int ContentTypeMaxLength = 128;

    /// <summary>A relative API path is short; generous headroom over the longest route in this API.</summary>
    private const int LocationMaxLength = 512;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("idempotency_records");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .ValueGeneratedNever();

        builder.Property(record => record.KeyHash)
            .IsRequired()
            .HasMaxLength(HashLength)
            .IsFixedLength();

        builder.Property(record => record.Caller)
            .IsRequired()
            .HasMaxLength(CallerMaxLength);

        builder.Property(record => record.FingerprintHash)
            .IsRequired()
            .HasMaxLength(HashLength)
            .IsFixedLength();

        builder.Property(record => record.CreatedAtUtc).IsRequired();
        builder.Property(record => record.ExpiresAtUtc).IsRequired();

        builder.Property(record => record.ResponseContentType)
            .HasMaxLength(ContentTypeMaxLength);

        // Unbounded: a response body is not a "name"-shaped column, and the default 256-char
        // convention (ApplicationDbContext.ConfigureConventions) would silently truncate it.
        builder.Property(record => record.ResponseBodyJson)
            .HasColumnType("text");

        builder.Property(record => record.ResponseLocation)
            .HasMaxLength(LocationMaxLength);

        // THE lookup every claim attempt performs, and the constraint that makes concurrent
        // duplicate claims resolve to exactly one winner (IIdempotencyStore.TryClaimAsync catches
        // the resulting unique-violation rather than relying on a read-then-write check, which two
        // simultaneous requests could both pass).
        builder.HasIndex(record => new { record.KeyHash, record.Caller })
            .IsUnique()
            .HasDatabaseName("ix_idempotency_records_key_hash_caller_unique");

        // Supports the retention purge's sweep (WHERE expires_at_utc <= @now).
        builder.HasIndex(record => record.ExpiresAtUtc)
            .HasDatabaseName("ix_idempotency_records_expires_at_utc");
    }
}
