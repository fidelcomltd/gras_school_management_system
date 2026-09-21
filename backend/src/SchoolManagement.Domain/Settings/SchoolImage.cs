using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One stored rendition of the school logo or head teacher signature (TASK-0005b stage B1; spec
/// 9.6). Rows are IMMUTABLE and never deleted — an upload writes new rows and repoints
/// <see cref="SchoolProfile"/> at them, and the old rows stay exactly so a publication snapshot
/// captured while they were current keeps rendering them forever (04-module-school-settings.md
/// line 77). There is no update method on this entity for that reason.
/// </summary>
/// <remarks>
/// <see cref="UploadGroupId"/> ties together every row produced by ONE upload: three rows (Original,
/// Size200, Size64) for a logo, one row (Original only) for a signature. <see cref="SchoolProfile"/>
/// stores the current upload's <see cref="UploadGroupId"/>, not an individual row id, so "the current
/// logo" always resolves to a consistent, complete set of renditions.
/// </remarks>
public sealed class SchoolImage : Entity<Guid>, IAuditableEntity
{
    /// <summary>Cloudinary's own <c>public_id</c> ceiling, and a safe bound for any future store.</summary>
    public const int AssetIdMaxLength = 255;

    /// <summary>Long enough for any real image MIME type; actual values are always <c>image/png</c> or <c>image/jpeg</c>.</summary>
    public const int ContentTypeMaxLength = 50;

    // EF Core materialisation constructor.
    private SchoolImage()
        : base()
    {
        AssetId = string.Empty;
        ContentType = string.Empty;
    }

    private SchoolImage(
        Guid id,
        SchoolImageKind kind,
        SchoolImageSizeVariant sizeVariant,
        Guid uploadGroupId,
        string assetId,
        int widthPixels,
        int heightPixels,
        string contentType)
        : base(id)
    {
        Kind = kind;
        SizeVariant = sizeVariant;
        UploadGroupId = uploadGroupId;
        AssetId = assetId;
        WidthPixels = widthPixels;
        HeightPixels = heightPixels;
        ContentType = contentType;
    }

    /// <summary>Logo or signature.</summary>
    public SchoolImageKind Kind { get; private set; }

    /// <summary>Which rendition this row is. Always <see cref="SchoolImageSizeVariant.Original"/> for a signature.</summary>
    public SchoolImageSizeVariant SizeVariant { get; private set; }

    /// <summary>
    /// Groups every row from the same upload (one logo upload produces three rows sharing one
    /// value; one signature upload produces one row). <see cref="SchoolProfile"/>'s pointers store
    /// this, not an individual row id.
    /// </summary>
    public Guid UploadGroupId { get; private set; }

    /// <summary>The opaque key <c>ISchoolImageStore.PutAsync</c> returned for this rendition's bytes.</summary>
    public string AssetId { get; private set; }

    /// <summary>Width of this rendition, in pixels, after any EXIF orientation correction.</summary>
    public int WidthPixels { get; private set; }

    /// <summary>Height of this rendition, in pixels, after any EXIF orientation correction.</summary>
    public int HeightPixels { get; private set; }

    /// <summary>Either <c>image/png</c> or <c>image/jpeg</c>.</summary>
    public string ContentType { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates one rendition row. The caller (stage B2's upload handler) has already run the file
    /// through <c>ISchoolImageProcessor</c> and <c>ISchoolImageStore</c> — this factory trusts its
    /// inputs, the same "no input validation in the domain layer" posture every other entity in this
    /// codebase takes (for example <c>RemarkTemplate.Create</c>).
    /// </summary>
    public static SchoolImage Create(
        Guid id,
        SchoolImageKind kind,
        SchoolImageSizeVariant sizeVariant,
        Guid uploadGroupId,
        string assetId,
        int widthPixels,
        int heightPixels,
        string contentType) =>
        new(id, kind, sizeVariant, uploadGroupId, assetId, widthPixels, heightPixels, contentType);
}
