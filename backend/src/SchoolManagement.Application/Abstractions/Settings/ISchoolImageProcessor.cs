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
