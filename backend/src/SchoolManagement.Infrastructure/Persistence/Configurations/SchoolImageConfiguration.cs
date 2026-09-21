using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="SchoolImage"/> (TASK-0005b stage B1; spec 9.6).</summary>
internal sealed class SchoolImageConfiguration : IEntityTypeConfiguration<SchoolImage>
{
    private const int AuditActorMaxLength = 128;
    private const int KindMaxLength = 20;
    private const int SizeVariantMaxLength = 20;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SchoolImage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("school_image");

        builder.HasKey(image => image.Id);
        builder.Property(image => image.Id).ValueGeneratedNever();

        builder.Property(image => image.Kind).HasConversion<string>().HasMaxLength(KindMaxLength).IsRequired();
        builder.Property(image => image.SizeVariant).HasConversion<string>().HasMaxLength(SizeVariantMaxLength).IsRequired();
        builder.Property(image => image.UploadGroupId).IsRequired();
        builder.Property(image => image.AssetId).HasMaxLength(SchoolImage.AssetIdMaxLength).IsRequired();
        builder.Property(image => image.WidthPixels).IsRequired();
        builder.Property(image => image.HeightPixels).IsRequired();
        builder.Property(image => image.ContentType).HasMaxLength(SchoolImage.ContentTypeMaxLength).IsRequired();

        builder.Property(image => image.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(image => image.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Rows are never updated (see the entity's remarks), but ModifiedAtUtc/ModifiedBy stay
        // mapped: IAuditableEntity is implemented in full, and a future defect that DID modify a row
        // should still be auditable rather than silently unrecorded.

        // Every rendition from one upload shares an UploadGroupId; SchoolProfile's current-pointer
        // columns resolve to the full set through this index, not a single row id.
        builder.HasIndex(image => new { image.UploadGroupId, image.SizeVariant })
            .HasDatabaseName("ix_school_image_upload_group_id_size_variant");

        // No foreign key to school_profile: the relationship is the other way round (school_profile
        // POINTS AT an upload group), and an old, no-longer-current row must stay exactly as it is
        // for as long as a publication snapshot references it — an FK from this table outward would
        // invite a cascade delete that Amendment 4 and 04 line 77 both forbid.
    }
}
