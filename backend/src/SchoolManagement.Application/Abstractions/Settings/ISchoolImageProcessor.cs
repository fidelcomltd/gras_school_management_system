using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Abstractions.Settings;

/// <summary>
/// Verifies and re-encodes a caller-supplied school logo or head teacher signature upload — the
/// seam that keeps the choice of image library (SkiaSharp) out of Application. Implemented in
/// Infrastructure. See spec 9.6 ("File uploads") and 04-module-school-settings.md.
/// </summary>
/// <remarks>
/// <para>
/// Every accepted file is decoded and RE-ENCODED from scratch, never copied through byte for byte.
/// That is what strips ALL metadata unconditionally — EXIF (including GPS, which can carry the
/// coordinates of wherever the photo was taken) and a PNG <c>eXIf</c> chunk alike — because the
/// encoder never writes a chunk it was not asked to write.
/// </para>
/// <para>
/// Content type is decided by the file's MAGIC BYTES, never a declared content type or a file
/// name/extension (spec 9.6, "All uploads"): neither is passed to this port at all. SVG, GIF, WebP
/// and anything else are refused outright — SVG because it is a script container, per spec 9.6.
/// </para>
/// <para>
/// Bytes are never persisted or transmitted here. This port only turns input bytes into output
/// bytes (or an <see cref="Error"/>); storage is <c>ISchoolImageStore</c> (TASK-0005b stage B).
/// </para>
/// </remarks>
public interface ISchoolImageProcessor
{
    /// <summary>
    /// Validates and processes a school logo upload.
    /// </summary>
    /// <param name="fileBytes">The raw uploaded bytes, exactly as received.</param>
    /// <returns>
    /// On success, three renditions — <see cref="SchoolImageSizeVariant.Original"/>,
    /// <see cref="SchoolImageSizeVariant.Size200"/> and <see cref="SchoolImageSizeVariant.Size64"/>
    /// (long edge, aspect kept) — with PNG transparency preserved. On failure, one of
    /// <see cref="SchoolImageErrorCodes.UnsupportedType"/> (not PNG/JPEG by magic bytes),
    /// <see cref="SchoolImageErrorCodes.TooLarge"/> (over
    /// <see cref="SchoolImageLimits.MaxLogoBytes"/>), or <see cref="SchoolImageErrorCodes.TooSmall"/>
    /// (under <see cref="SchoolImageLimits.MinLogoDimensionPixels"/> on either edge).
    /// </returns>
    Result<ProcessedSchoolImage> ProcessLogo(byte[] fileBytes);

    /// <summary>
    /// Validates and processes a head teacher signature upload.
    /// </summary>
    /// <param name="fileBytes">The raw uploaded bytes, exactly as received.</param>
    /// <returns>
    /// On success, one <see cref="SchoolImageSizeVariant.Original"/> rendition — re-encoded to strip
    /// metadata, but NEVER resized (600x200 is a recommendation, not a requirement). On failure,
    /// <see cref="SchoolImageErrorCodes.UnsupportedType"/> or <see cref="SchoolImageErrorCodes.TooLarge"/>
    /// (over <see cref="SchoolImageLimits.MaxSignatureBytes"/>) — there is no minimum dimension.
    /// </returns>
    Result<ProcessedSchoolImage> ProcessSignature(byte[] fileBytes);

    /// <summary>
    /// Validates and processes a pupil photograph (spec 6.5.4, 9.6): a 400 by 400 centre-cropped JPEG at quality 80 and a
    /// 96 pixel square thumbnail. The original is not returned, so it cannot be stored.
    /// </summary>
    /// <param name="fileBytes">The raw uploaded bytes, exactly as received.</param>
    /// <returns>
    /// On failure, <see cref="PupilUploadErrorCodes.PhotoUnsupportedType"/> or <see cref="PupilUploadErrorCodes.PhotoTooLarge"/>
    /// (over <see cref="SchoolImageLimits.MaxPupilPhotoBytes"/>, with the spec's own message naming the file's size).
    /// </returns>
    Result<ProcessedPupilPhoto> ProcessPupilPhoto(byte[] fileBytes);

    /// <summary>
    /// Validates a document-checklist scan (spec 6.5.8): PDF, JPEG or PNG by magic bytes, at most
    /// <see cref="SchoolImageLimits.MaxDocumentScanBytes"/>. A JPEG or PNG is re-encoded at its own size, which strips EXIF
    /// and GPS (a phone photograph of a birth certificate carries where it was taken); a PDF is returned as it came.
    /// </summary>
    /// <param name="fileBytes">The raw uploaded bytes, exactly as received.</param>
    /// <returns>
    /// On failure, <see cref="PupilUploadErrorCodes.DocumentUnsupportedType"/> or <see cref="PupilUploadErrorCodes.DocumentTooLarge"/>.
    /// </returns>
    Result<ProcessedDocumentScan> ProcessDocumentScan(byte[] fileBytes);
}

/// <summary>The two stored renditions of a pupil photograph, both <c>image/jpeg</c>.</summary>
/// <param name="Standard">400 by 400, centre-cropped.</param>
/// <param name="Thumbnail">96 by 96, from the same crop.</param>
public sealed record ProcessedPupilPhoto(SchoolImageRendition Standard, SchoolImageRendition Thumbnail);

/// <summary>A checked document scan ready for <c>ISchoolImageStore</c>.</summary>
/// <param name="Bytes">Metadata-free for an image; unchanged for a PDF.</param>
/// <param name="ContentType"><c>application/pdf</c>, <c>image/jpeg</c> or <c>image/png</c>.</param>
public sealed record ProcessedDocumentScan(ReadOnlyMemory<byte> Bytes, string ContentType);

/// <summary>Stable error codes for pupil photograph and document scan uploads (the contract's <c>422</c>s).</summary>
public static class PupilUploadErrorCodes
{
    /// <summary>A photograph that is not PNG or JPEG by magic bytes.</summary>
    public const string PhotoUnsupportedType = "pupil_photo.unsupported_type";

    /// <summary>A photograph over 3 MB.</summary>
    public const string PhotoTooLarge = "pupil_photo.too_large";

    /// <summary>A scan that is not PDF, PNG or JPEG by magic bytes.</summary>
    public const string DocumentUnsupportedType = "pupil_document.unsupported_type";

    /// <summary>A scan over 5 MB.</summary>
    public const string DocumentTooLarge = "pupil_document.too_large";
}

/// <summary>The size an <see cref="ISchoolImageProcessor"/> rendition was produced at.</summary>
public enum SchoolImageSizeVariant
{
    /// <summary>The uploaded image at its native pixel dimensions, re-encoded.</summary>
    Original,

    /// <summary>Logo derivative, 200 pixels on the long edge.</summary>
    Size200,

    /// <summary>Logo derivative, 64 pixels on the long edge.</summary>
    Size64,

    /// <summary>Pupil photograph, 400 by 400.</summary>
    Square400,

    /// <summary>Pupil photograph thumbnail, 96 by 96.</summary>
    Square96,
}

/// <summary>One processed, re-encoded image ready to hand to <c>ISchoolImageStore</c>.</summary>
/// <param name="SizeVariant">Which size this rendition is.</param>
/// <param name="Bytes">
/// The re-encoded, metadata-free image bytes. <see cref="ReadOnlyMemory{T}"/> rather than
/// <c>byte[]</c> so this public record does not hand every caller a directly mutable array (CA1819).
/// </param>
/// <param name="WidthPixels">Width of this rendition, in pixels.</param>
/// <param name="HeightPixels">Height of this rendition, in pixels.</param>
/// <param name="ContentType">Either <c>image/png</c> or <c>image/jpeg</c>, matching the input format.</param>
public sealed record SchoolImageRendition(
    SchoolImageSizeVariant SizeVariant,
    ReadOnlyMemory<byte> Bytes,
    int WidthPixels,
    int HeightPixels,
    string ContentType);

/// <summary>
/// The full output of one successful <see cref="ISchoolImageProcessor"/> call: one rendition for a
/// signature, three for a logo.
/// </summary>
/// <param name="Renditions">
/// Always contains exactly one <see cref="SchoolImageSizeVariant.Original"/> entry, plus (for a
/// logo) one <see cref="SchoolImageSizeVariant.Size200"/> and one
/// <see cref="SchoolImageSizeVariant.Size64"/>.
/// </param>
public sealed record ProcessedSchoolImage(IReadOnlyList<SchoolImageRendition> Renditions)
{
    /// <summary>The native-resolution rendition, present on every successful result.</summary>
    public SchoolImageRendition Original =>
        Renditions.Single(rendition => rendition.SizeVariant == SchoolImageSizeVariant.Original);
}

/// <summary>Byte and pixel caps enforced by <see cref="ISchoolImageProcessor"/> (spec 9.6).</summary>
public static class SchoolImageLimits
{
    /// <summary>Maximum accepted logo upload size, in bytes.</summary>
    public const long MaxLogoBytes = 2 * 1024 * 1024;

    /// <summary>Maximum accepted signature upload size, in bytes. There is no minimum.</summary>
    public const long MaxSignatureBytes = 1 * 1024 * 1024;

    /// <summary>Minimum width and height a logo must meet, in pixels.</summary>
    public const int MinLogoDimensionPixels = 300;

    /// <summary>Maximum accepted pupil photograph upload, in bytes (the client downscales to 800 pixels first).</summary>
    public const long MaxPupilPhotoBytes = 3 * 1024 * 1024;

    /// <summary>Maximum accepted document scan upload, in bytes.</summary>
    public const long MaxDocumentScanBytes = 5 * 1024 * 1024;
}

/// <summary>
/// Stable error codes an <see cref="ISchoolImageProcessor"/> failure carries — the values the
/// contract's <c>422</c> responses use (TASK-0005b delta item 1).
/// </summary>
public static class SchoolImageErrorCodes
{
    /// <summary>Not PNG or JPEG by magic bytes (includes SVG, GIF, WebP, and a mislabelled file).</summary>
    public const string UnsupportedType = "school_image.unsupported_type";

    /// <summary>Over the upload's byte cap.</summary>
    public const string TooLarge = "school_image.too_large";

    /// <summary>Logo only: under the minimum pixel dimensions.</summary>
    public const string TooSmall = "school_image.too_small";
}
