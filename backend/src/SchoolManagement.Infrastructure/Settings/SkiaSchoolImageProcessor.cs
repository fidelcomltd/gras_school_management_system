using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Domain.Common;
using SkiaSharp;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// <see cref="ISchoolImageProcessor"/> over SkiaSharp (TASK-0005b stage A; human ruling
/// 2026-09-21: SkiaSharp, MIT licensed).
/// </summary>
/// <remarks>
/// Every rendition is produced by decoding the source once and re-encoding a (possibly resized)
/// bitmap from scratch through <see cref="SKImage.Encode(SKEncodedImageFormat, int)"/>. SkiaSharp's
/// encoder writes only pixel data plus the container format's own required structural chunks — it
/// never carries an input EXIF block or PNG <c>eXIf</c> chunk across, so metadata stripping falls
/// out of the re-encode rather than needing a separate "remove metadata" step.
/// <para>
/// TASK-0005b stage B1 (review finding on stage A): <see cref="SKCodec.EncodedOrigin"/> is read and
/// applied to the PIXELS, by rotating/flipping into a fresh bitmap, BEFORE that re-encode. Stage A's
/// re-encode already stripped the EXIF <c>Orientation</c> tag along with everything else — correct
/// on the "no metadata survives" requirement, but it left a portrait phone photo stored sideways
/// with nothing left to say so. Applying the correction first and stripping second gets both.
/// </para>
/// </remarks>
internal sealed class SkiaSchoolImageProcessor : ISchoolImageProcessor
{
    private const string PngContentType = "image/png";
    private const string JpegContentType = "image/jpeg";
    private const int JpegQuality = 90;
    private const int PupilPhotoJpegQuality = 80;
    private const string PdfContentType = "application/pdf";

    // Magic-byte signatures (spec 9.6: content type is decided by inspecting the file's bytes, never
    // a declared content type or extension — this port is never even given a file name).
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    /// <inheritdoc />
    public Result<ProcessedSchoolImage> ProcessLogo(byte[] fileBytes)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);

        var sizeCheck = CheckMaxSize(fileBytes, SchoolImageLimits.MaxLogoBytes);
        if (sizeCheck.IsFailure)
        {
            return Result.Failure<ProcessedSchoolImage>(sizeCheck.Error);
        }

        var decoded = DecodeAndDetectType(fileBytes);
        if (decoded.IsFailure)
        {
            return Result.Failure<ProcessedSchoolImage>(decoded.Error);
        }

        using var bitmap = decoded.Value.Bitmap;
        var contentType = decoded.Value.ContentType;

        if (bitmap.Width < SchoolImageLimits.MinLogoDimensionPixels ||
            bitmap.Height < SchoolImageLimits.MinLogoDimensionPixels)
        {
            return Result.Failure<ProcessedSchoolImage>(TooSmall());
        }

        List<SchoolImageRendition> renditions =
        [
            Encode(bitmap, contentType, SchoolImageSizeVariant.Original),
            EncodeResized(bitmap, contentType, SchoolImageSizeVariant.Size200, longEdgePixels: 200),
            EncodeResized(bitmap, contentType, SchoolImageSizeVariant.Size64, longEdgePixels: 64),
        ];

        return Result.Success(new ProcessedSchoolImage(renditions));
    }

    /// <inheritdoc />
    public Result<ProcessedSchoolImage> ProcessSignature(byte[] fileBytes)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);

        var sizeCheck = CheckMaxSize(fileBytes, SchoolImageLimits.MaxSignatureBytes);
        if (sizeCheck.IsFailure)
        {
            return Result.Failure<ProcessedSchoolImage>(sizeCheck.Error);
        }

        var decoded = DecodeAndDetectType(fileBytes);
        if (decoded.IsFailure)
        {
            return Result.Failure<ProcessedSchoolImage>(decoded.Error);
        }

        // Never resized: 600x200 is only a recommendation, and a signature is rendered at a fixed
        // height wherever it is printed (spec 9.6), so there is no derivative to produce here.
        using var bitmap = decoded.Value.Bitmap;
        List<SchoolImageRendition> renditions = [Encode(bitmap, decoded.Value.ContentType, SchoolImageSizeVariant.Original)];

        return Result.Success(new ProcessedSchoolImage(renditions));
    }

    /// <inheritdoc />
    public Result<ProcessedPupilPhoto> ProcessPupilPhoto(byte[] fileBytes)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);

        if (fileBytes.LongLength > SchoolImageLimits.MaxPupilPhotoBytes)
        {
            return Result.Failure<ProcessedPupilPhoto>(Error.Validation(
                PupilUploadErrorCodes.PhotoTooLarge,
                $"This photograph is {Megabytes(fileBytes.LongLength)} MB. The limit is {SchoolImageLimits.MaxPupilPhotoBytes / (1024 * 1024)} MB. " +
                "Reduce the size or take the photograph again at a lower quality."));
        }

        var decoded = DecodeAndDetectType(fileBytes);
        if (decoded.IsFailure)
        {
            return Result.Failure<ProcessedPupilPhoto>(Error.Validation(
                PupilUploadErrorCodes.PhotoUnsupportedType, "Only PNG and JPEG photographs are accepted, verified by file content rather than name."));
        }

        // Centre crop to the largest square, then both sizes come from that one crop. The original is dropped here.
        using var bitmap = decoded.Value.Bitmap;
        var side = Math.Min(bitmap.Width, bitmap.Height);
        using var square = new SKBitmap();
        bitmap.ExtractSubset(square, SKRectI.Create((bitmap.Width - side) / 2, (bitmap.Height - side) / 2, side, side));

        return Result.Success(new ProcessedPupilPhoto(
            EncodeSquareJpeg(square, 400, SchoolImageSizeVariant.Square400),
            EncodeSquareJpeg(square, 96, SchoolImageSizeVariant.Square96)));
    }

    /// <inheritdoc />
    public Result<ProcessedDocumentScan> ProcessDocumentScan(byte[] fileBytes)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);

        if (fileBytes.LongLength > SchoolImageLimits.MaxDocumentScanBytes)
        {
            return Result.Failure<ProcessedDocumentScan>(Error.Validation(
                PupilUploadErrorCodes.DocumentTooLarge,
                $"This file is {Megabytes(fileBytes.LongLength)} MB. The limit is {SchoolImageLimits.MaxDocumentScanBytes / (1024 * 1024)} MB."));
        }

        // A PDF cannot be re-encoded here, so it is stored as it came; it is only ever served as an attachment.
        if (fileBytes.AsSpan().StartsWith(PdfMagic))
        {
            return Result.Success(new ProcessedDocumentScan(fileBytes, PdfContentType));
        }

        var decoded = DecodeAndDetectType(fileBytes);
        if (decoded.IsFailure)
        {
            return Result.Failure<ProcessedDocumentScan>(Error.Validation(
                PupilUploadErrorCodes.DocumentUnsupportedType, "Only PDF, JPEG and PNG files are accepted, verified by file content rather than name."));
        }

        using var bitmap = decoded.Value.Bitmap;
        return Result.Success(new ProcessedDocumentScan(EncodeBitmap(bitmap, decoded.Value.ContentType), decoded.Value.ContentType));
    }

    /// <summary>Rounded UP to one decimal place, so a file just over the limit never reads as exactly the limit.</summary>
    private static string Megabytes(long bytes) =>
        (Math.Ceiling(bytes * 10.0 / (1024 * 1024)) / 10).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Resizes a square to <paramref name="size"/> and flattens it onto white: JPEG has no alpha, and a transparent PNG
    /// pixel would otherwise encode as black.
    /// </summary>
    private static SchoolImageRendition EncodeSquareJpeg(SKBitmap square, int size, SchoolImageSizeVariant variant)
    {
        var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var resized = square.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        using var flattened = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(flattened))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(resized, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        }

        using var image = SKImage.FromBitmap(flattened);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, PupilPhotoJpegQuality);
        return new SchoolImageRendition(variant, encoded.ToArray(), size, size, JpegContentType);
    }

    private static Result CheckMaxSize(byte[] fileBytes, long maxBytes) =>
        fileBytes.LongLength > maxBytes ? Result.Failure(TooLarge(maxBytes)) : Result.Success();

    private static Result<(SKBitmap Bitmap, string ContentType)> DecodeAndDetectType(byte[] fileBytes)
    {
        var contentType = DetectContentType(fileBytes);
        if (contentType is null)
        {
            return Result.Failure<(SKBitmap, string)>(UnsupportedType());
        }

        using var data = SKData.CreateCopy(fileBytes);
        using var codec = SKCodec.Create(data);
        var rawBitmap = SKBitmap.Decode(codec);

        if (rawBitmap is null)
        {
            return Result.Failure<(SKBitmap, string)>(UnsupportedType());
        }

        // CA2000 cannot see across the Result boundary: ownership of whichever bitmap this method
        // returns transfers to the caller, which disposes it via `using var bitmap = decoded.Value.Bitmap;`
        // (see ProcessLogo/ProcessSignature) — the same ownership-transfer shape SKBitmap.Decode
        // itself already had before this method wrapped it.
#pragma warning disable CA2000
        var orientedBitmap = ApplyExifOrientation(rawBitmap, codec.EncodedOrigin);
#pragma warning restore CA2000

        if (!ReferenceEquals(orientedBitmap, rawBitmap))
        {
            rawBitmap.Dispose();
        }

        return Result.Success((orientedBitmap, contentType));
    }

    /// <summary>
    /// Rotates/flips <paramref name="source"/>'s PIXELS into a fresh bitmap matching what
    /// <paramref name="origin"/> (the file's own EXIF <c>Orientation</c> tag, as SkiaSharp read it)
    /// says the image should look like once displayed correctly. Returns <paramref name="source"/>
    /// itself, unchanged, when no correction is needed — the common case.
    /// </summary>
    /// <remarks>
    /// The four "swap" origins (5-8) exchange width and height, matching the two possible 90-degree
    /// rotations: a stored landscape photo tagged orientation 6 comes out portrait, and vice versa.
    /// </remarks>
    private static SKBitmap ApplyExifOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return source;
        }

        var swapsDimensions = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        var oriented = new SKBitmap(
            swapsDimensions ? source.Height : source.Width,
            swapsDimensions ? source.Width : source.Height,
            source.ColorType,
            source.AlphaType);

        using (var canvas = new SKCanvas(oriented))
        {
            switch (origin)
            {
                case SKEncodedOrigin.TopRight: // 2: mirror horizontal.
                    canvas.Translate(oriented.Width, 0);
                    canvas.Scale(-1, 1);
                    break;
                case SKEncodedOrigin.BottomRight: // 3: rotate 180.
                    canvas.Translate(oriented.Width, oriented.Height);
                    canvas.RotateDegrees(180);
                    break;
                case SKEncodedOrigin.BottomLeft: // 4: mirror vertical.
                    canvas.Translate(0, oriented.Height);
                    canvas.Scale(1, -1);
                    break;
                case SKEncodedOrigin.LeftTop: // 5: transpose (mirror horizontal, then rotate 270 CW).
                    canvas.RotateDegrees(90);
                    canvas.Scale(1, -1);
                    break;
                case SKEncodedOrigin.RightTop: // 6: rotate 90 CW.
                    canvas.Translate(oriented.Width, 0);
                    canvas.RotateDegrees(90);
                    break;
                case SKEncodedOrigin.RightBottom: // 7: transverse (mirror horizontal, then rotate 90 CW).
                    canvas.Translate(oriented.Width, oriented.Height);
                    canvas.RotateDegrees(90);
                    canvas.Scale(-1, 1);
                    break;
                case SKEncodedOrigin.LeftBottom: // 8: rotate 270 CW.
                    canvas.Translate(0, oriented.Height);
                    canvas.RotateDegrees(-90);
                    break;
                default:
                    break;
            }

            canvas.DrawBitmap(source, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        }

        return oriented;
    }

    /// <summary>
    /// PNG and JPEG signatures only. Anything else — SVG, GIF, WebP, a truncated or corrupt file, or
    /// a differently-typed file wearing a misleading name — is refused, because this method is the
    /// only thing consulted and it never sees a name or a declared type.
    /// </summary>
    private static string? DetectContentType(byte[] fileBytes)
    {
        if (fileBytes.Length >= PngMagic.Length &&
            fileBytes.AsSpan(0, PngMagic.Length).SequenceEqual(PngMagic))
        {
            return PngContentType;
        }

        if (fileBytes.Length >= JpegMagic.Length &&
            fileBytes.AsSpan(0, JpegMagic.Length).SequenceEqual(JpegMagic))
        {
            return JpegContentType;
        }

        return null;
    }

    private static SchoolImageRendition Encode(SKBitmap bitmap, string contentType, SchoolImageSizeVariant variant) =>
        new(variant, EncodeBitmap(bitmap, contentType), bitmap.Width, bitmap.Height, contentType);

    private static SchoolImageRendition EncodeResized(
        SKBitmap source, string contentType, SchoolImageSizeVariant variant, int longEdgePixels)
    {
        var scale = (double)longEdgePixels / Math.Max(source.Width, source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale, MidpointRounding.AwayFromZero));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale, MidpointRounding.AwayFromZero));

        var info = new SKImageInfo(width, height, source.ColorType, source.AlphaType);
        using var resized = source.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        return new SchoolImageRendition(variant, EncodeBitmap(resized, contentType), resized.Width, resized.Height, contentType);
    }

    private static byte[] EncodeBitmap(SKBitmap bitmap, string contentType)
    {
        using var image = SKImage.FromBitmap(bitmap);
        var format = contentType == PngContentType ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
        using var encoded = image.Encode(format, JpegQuality);
        return encoded.ToArray();
    }

    private static Error TooLarge(long maxBytes) => Error.Validation(
        SchoolImageErrorCodes.TooLarge,
        $"The file exceeds the maximum size of {maxBytes / (1024 * 1024)} MB.");

    private static Error TooSmall() => Error.Validation(
        SchoolImageErrorCodes.TooSmall,
        $"The logo must be at least {SchoolImageLimits.MinLogoDimensionPixels} by " +
        $"{SchoolImageLimits.MinLogoDimensionPixels} pixels.");

    private static Error UnsupportedType() => Error.Validation(
        SchoolImageErrorCodes.UnsupportedType,
        "Only PNG and JPEG images are accepted, verified by file content rather than name.");
}
