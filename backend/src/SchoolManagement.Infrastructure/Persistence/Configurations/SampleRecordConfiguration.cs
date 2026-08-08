using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Reference;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// REFERENCE SCAFFOLD — mapping for <see cref="SampleRecord"/>. Copy this file's SHAPE for real
/// entities; delete it with the entity.
/// </summary>
/// <remarks>
/// The conventions demonstrated here apply to every entity configuration in the codebase:
/// explicit table name, explicit key, column lengths taken from domain constants, a concurrency
/// token, and a FILTERED unique index because the entity is soft-deletable.
/// </remarks>
internal sealed class SampleRecordConfiguration : IEntityTypeConfiguration<SampleRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SampleRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Table names are explicit and plural. Column names come from the snake_case convention, so
        // this maps to "sample_records" with columns "id", "label", "created_at_utc", and so on.
        builder.ToTable("sample_records");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            // The application supplies a v7 GUID; the database must not overwrite it with its own
            // default, or the value the command returned to the client would not match the row.
            .ValueGeneratedNever();

        builder.Property(record => record.Label)
            .IsRequired()
            .HasMaxLength(SampleRecord.LabelMaxLength);

        builder.Property(record => record.Note)
            .HasMaxLength(SampleRecord.NoteMaxLength);

        builder.Property(record => record.CreatedAtUtc)
            .IsRequired();

        builder.Property(record => record.CreatedBy)
            .HasMaxLength(AuditActorMaxLength);

        builder.Property(record => record.ModifiedBy)
            .HasMaxLength(AuditActorMaxLength);

        builder.Property(record => record.DeletedBy)
            .HasMaxLength(AuditActorMaxLength);

        builder.Property(record => record.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // NOTE: no concurrency token is configured here. It is applied to every auditable entity by
        // convention in ApplicationDbContext.ApplyConcurrencyTokens, so a new entity gets optimistic
        // concurrency without anyone remembering to opt in. See that method for why it is an
        // application-maintained GUID rather than PostgreSQL's xmin.

        // FILTERED unique index — the filter is REQUIRED, not an optimisation. Without
        // "WHERE NOT is_deleted", a soft-deleted row would keep reserving its label forever and a
        // user could never reuse the label of something they deleted.
        builder.HasIndex(record => record.Label)
            .IsUnique()
            .HasFilter("NOT is_deleted")
            .HasDatabaseName("ix_sample_records_label_unique_live");

        // Supports the default listing order (newest first) without a sort.
        builder.HasIndex(record => record.CreatedAtUtc)
            .HasDatabaseName("ix_sample_records_created_at_utc");
    }

    /// <summary>
    /// Maximum length of an audit actor column. Sized for an external identity-provider subject
    /// identifier, which can be considerably longer than a username.
    /// </summary>
    private const int AuditActorMaxLength = 128;
}
