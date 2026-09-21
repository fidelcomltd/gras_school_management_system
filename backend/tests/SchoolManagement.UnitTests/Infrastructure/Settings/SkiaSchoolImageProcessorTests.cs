using System.Text;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Infrastructure.Settings;
using SkiaSharp;

namespace SchoolManagement.UnitTests.Infrastructure.Settings;

/// <summary>
/// Tests <see cref="SkiaSchoolImageProcessor"/> against every TASK-0005b stage A acceptance
/// criterion (spec 9.6): magic-byte type detection, the size caps, metadata stripping, and the
/// logo/signature resizing rules.
/// </summary>
public sealed class SkiaSchoolImageProcessorTests
{
    private readonly SkiaSchoolImageProcessor _processor = new();

    // --- Magic bytes decide the type -----------------------------------------------------------

    [Fact]
    public void ProcessLogo_WithAValidPng_Succeeds()
    {
        var result = _processor.ProcessLogo(CreatePng(320, 320));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Original.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public void ProcessLogo_WithAValidJpeg_Succeeds()
    {
        var result = _processor.ProcessLogo(CreateJpeg(320, 320));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Original.ContentType.ShouldBe("image/jpeg");
    }

    [Fact]
    public void ProcessLogo_DecidesTypeByMagicBytesAlone()
    {
        // This port is never given a file name or a declared content type — the only way it could
        // possibly be fooled by "a PNG renamed .jpg" is if it looked at something other than bytes.
        // It doesn't take a name parameter at all, so real PNG bytes are read as PNG regardless of
        // what any caller might have called the file.
        var result = _processor.ProcessLogo(CreatePng(320, 320));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Original.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public void ProcessLogo_WithSvgBytes_IsRefused()
    {
        var svgBytes = "<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"u8.ToArray();

        var result = _processor.ProcessLogo(svgBytes);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SchoolImageErrorCodes.UnsupportedType);
    }

    [Fact]
    public void ProcessLogo_WithGifBytes_IsRefused()
    {
        var gifBytes = "GIF89a"u8.ToArray();

        var result = _processor.ProcessLogo(gifBytes);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SchoolImageErrorCodes.UnsupportedType);
    }

    [Fact]
    public void ProcessLogo_WithWebpBytes_IsRefused()
    {
        byte[] webpBytes = [.. "RIFF"u8.ToArray(), 0, 0, 0, 0, .. "WEBP"u8.ToArray()];

        var result = _processor.ProcessLogo(webpBytes);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SchoolImageErrorCodes.UnsupportedType);
    }

    [Fact]
    public void ProcessLogo_WithGarbageBytes_IsRefused()
    {
        var result = _processor.ProcessLogo([1, 2, 3, 4, 5]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SchoolImageErrorCodes.UnsupportedType);
    }

    // --- Caps ------------------------------------------------------------------------------------

    [Fact]
    public void ProcessLogo_OverTwoMegabytes_IsRefusedAsTooLarge()
    {
        var oversized = new byte[SchoolImageLimits.MaxLogoBytes + 1];

        var result = _processor.ProcessLogo(oversized);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SchoolImageErrorCodes.TooLarge);
    }

    [Fact]
    public void ProcessSignature_OverOneMegabyte_IsRefusedAsTooLarge()
    {
        var oversized = new byte[SchoolImageLimits.MaxSignatureBytes + 1];

        var result = _processor.ProcessSignature(oversized);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SchoolImageErrorCodes.TooLarge);
    }

    [Fact]
    public void ProcessLogo_AtExactlyTheMinimumDimensions_Succeeds()
    {
        var result = _processor.ProcessLogo(CreatePng(300, 300));

        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(299, 300)]
    [InlineData(300, 299)]
    [InlineData(150, 150)]
    public void ProcessLogo_BelowTheMinimumDimensions_IsRefusedAsTooSmall(int width, int height)
    {
        var result = _processor.ProcessLogo(CreatePng(width, height));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SchoolImageErrorCodes.TooSmall);
    }

    [Fact]
    public void ProcessSignature_HasNoMinimumDimension_ATinyImageSucceeds()
    {
        var result = _processor.ProcessSignature(CreatePng(10, 8));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Original.WidthPixels.ShouldBe(10);
        result.Value.Original.HeightPixels.ShouldBe(8);
    }

    // --- Metadata stripping ------------------------------------------------------------------

    [Fact]
    public void ProcessLogo_WithAJpegCarryingExifGps_StripsAllMetadata()
    {
        var sourceBytes = ReadEmbeddedFixture("logo-with-gps-exif.jpg");
        JpegHasExifSegment(sourceBytes).ShouldBeTrue("the fixture must actually carry EXIF, or this test proves nothing");

        var result = _processor.ProcessLogo(sourceBytes);

        result.IsSuccess.ShouldBeTrue();
        var outputBytes = result.Value.Original.Bytes.ToArray();
        JpegHasExifSegment(outputBytes).ShouldBeFalse("re-encoded output must carry no EXIF (APP1) segment at all");
    }

    [Fact]
    public void ProcessLogo_WithAPngCarryingAnEXifChunk_StripsAllMetadata()
    {
        var sourceBytes = ReadEmbeddedFixture("logo-with-exif-chunk.png");
        PngHasChunk(sourceBytes, "eXIf").ShouldBeTrue("the fixture must actually carry an eXIf chunk, or this test proves nothing");

        var result = _processor.ProcessLogo(sourceBytes);

        result.IsSuccess.ShouldBeTrue();
        var outputBytes = result.Value.Original.Bytes.ToArray();
        PngHasChunk(outputBytes, "eXIf").ShouldBeFalse("re-encoded output must carry no eXIf chunk");
    }

    // --- Orientation (stage B1: review finding on stage A) -----------------------------------

    [Fact]
    public void ProcessSignature_WithExifOrientationSix_RotatesPixelsBeforeStrippingMetadata()
    {
        // Fixture: 40x30 AS STORED, a 4x4 red marker at the STORED top-left corner, tagged EXIF
        // orientation 6 ("rotate 90 CW to display correctly"). A camera that shot this in portrait
        // and stored it landscape-with-a-tag is exactly the scenario this stage exists for.
        var sourceBytes = ReadEmbeddedFixture("orientation-6.jpg");

        var result = _processor.ProcessSignature(sourceBytes);

        result.IsSuccess.ShouldBeTrue();
        var original = result.Value.Original;

        // Rotating 40x30 stored pixels 90 degrees CW gives 30x40 displayed pixels — swapped, not
        // just re-encoded at the stored dimensions.
        original.WidthPixels.ShouldBe(30);
        original.HeightPixels.ShouldBe(40);

        // Physically rotating an image 90 CW moves its top-left corner to the top-right. The output
        // has no EXIF any more (stage A), so this decode reads raw pixels with no correction applied
        // — if the marker is where a 90 CW rotation predicts, the correction actually happened before
        // the encode, not merely a claim.
        using var decoded = SKBitmap.Decode(original.Bytes.ToArray());
        var topRight = decoded.GetPixel(decoded.Width - 1, 0);
        var topLeft = decoded.GetPixel(0, 0);
        var bottomRight = decoded.GetPixel(decoded.Width - 1, decoded.Height - 1);

        IsReddish(topRight).ShouldBeTrue($"expected the marker at the rotated top-right, got {topRight}");
        IsBlueish(topLeft).ShouldBeTrue($"expected background at the top-left, got {topLeft}");
        IsBlueish(bottomRight).ShouldBeTrue($"expected background at the bottom-right, got {bottomRight}");
    }

    [Fact]
    public void ProcessSignature_WithNoExifOrientationTag_LeavesPixelsUnchanged()
    {
        // CreatePng carries no EXIF at all, so SKCodec.EncodedOrigin is TopLeft (the default) and no
        // rotation should be applied — dimensions must come out exactly as authored.
        var result = _processor.ProcessSignature(CreatePng(600, 200));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Original.WidthPixels.ShouldBe(600);
        result.Value.Original.HeightPixels.ShouldBe(200);
    }

    private static bool IsReddish(SKColor color) => color.Red > 120 && color.Blue < 120;

    private static bool IsBlueish(SKColor color) => color.Blue > 120 && color.Red < 120;

    // --- Transparency and derivatives ---------------------------------------------------------

    [Fact]
    public void ProcessLogo_WithPngTransparency_PreservesIt()
    {
        var sourceBytes = CreateTransparentPng(320, 320);

        var result = _processor.ProcessLogo(sourceBytes);

        result.IsSuccess.ShouldBeTrue();
        using var decoded = SKBitmap.Decode(result.Value.Original.Bytes.ToArray());
        decoded.GetPixel(0, 0).Alpha.ShouldBe((byte)0);
        decoded.GetPixel(decoded.Width - 1, decoded.Height - 1).Alpha.ShouldBe((byte)255);
    }

    [Fact]
    public void ProcessLogo_ProducesOriginalPlusTwoDerivativesWithAspectKept()
    {
        var result = _processor.ProcessLogo(CreatePng(400, 320));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Renditions.Count.ShouldBe(3);

        var original = result.Value.Original;
        original.WidthPixels.ShouldBe(400);
        original.HeightPixels.ShouldBe(320);

        var size200 = result.Value.Renditions.Single(r => r.SizeVariant == SchoolImageSizeVariant.Size200);
        size200.WidthPixels.ShouldBe(200);
        size200.HeightPixels.ShouldBe(160);

        var size64 = result.Value.Renditions.Single(r => r.SizeVariant == SchoolImageSizeVariant.Size64);
        size64.WidthPixels.ShouldBe(64);
        size64.HeightPixels.ShouldBe(51);
    }

    [Fact]
    public void ProcessSignature_IsNeverResized()
    {
        var result = _processor.ProcessSignature(CreatePng(600, 200));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Renditions.Count.ShouldBe(1);
        result.Value.Original.WidthPixels.ShouldBe(600);
        result.Value.Original.HeightPixels.ShouldBe(200);
    }

    // --- Fixtures and helpers ------------------------------------------------------------------

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(30, 90, 160));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] CreateTransparentPng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawRect(
            SKRect.Create(width / 2f, height / 2f, width / 2f, height / 2f),
            new SKPaint { Color = SKColors.Red, IsAntialias = false });
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] CreateJpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(200, 60, 40));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    private static byte[] ReadEmbeddedFixture(string fileName)
    {
        var assembly = typeof(SkiaSchoolImageProcessorTests).Assembly;
        var resourceName = $"SchoolManagement.UnitTests.Infrastructure.Settings.Fixtures.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded fixture '{resourceName}' not found. Available: " +
                string.Join(", ", assembly.GetManifestResourceNames()));
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Walks JPEG markers looking for an APP1 segment whose payload starts with the EXIF signature
    /// (<c>Exif\0\0</c>). Independent of the production decoder: this reads the JPEG container
    /// structure directly, so it can actually fail if re-encoding ever stopped stripping metadata.
    /// </summary>
    private static bool JpegHasExifSegment(byte[] jpeg)
    {
        var offset = 2; // Skip the SOI marker (FF D8).

        while (offset + 4 <= jpeg.Length && jpeg[offset] == 0xFF)
        {
            var marker = jpeg[offset + 1];

            if (marker == 0xD9 || marker == 0xDA)
            {
                // EOI or start-of-scan: no more markers carry metadata.
                break;
            }

            var segmentLength = (jpeg[offset + 2] << 8) | jpeg[offset + 3];

            if (marker == 0xE1 && offset + 4 + 6 <= jpeg.Length)
            {
                var payloadStart = offset + 4;
                var isExif = jpeg[payloadStart] == 'E' && jpeg[payloadStart + 1] == 'x' &&
                    jpeg[payloadStart + 2] == 'i' && jpeg[payloadStart + 3] == 'f';

                if (isExif)
                {
                    return true;
                }
            }

            offset += 2 + segmentLength;
        }

        return false;
    }

    /// <summary>
    /// Walks PNG chunks looking for one of type <paramref name="chunkType"/> (for example
    /// <c>eXIf</c>). Independent of the production decoder, for the same reason as
    /// <see cref="JpegHasExifSegment"/>.
    /// </summary>
    private static bool PngHasChunk(byte[] png, string chunkType)
    {
        var offset = 8; // Skip the 8-byte PNG signature.
        var typeBytes = Encoding.ASCII.GetBytes(chunkType);

        while (offset + 8 <= png.Length)
        {
            var length = (png[offset] << 24) | (png[offset + 1] << 16) | (png[offset + 2] << 8) | png[offset + 3];
            var typeOffset = offset + 4;

            if (png[typeOffset] == typeBytes[0] && png[typeOffset + 1] == typeBytes[1] &&
                png[typeOffset + 2] == typeBytes[2] && png[typeOffset + 3] == typeBytes[3])
            {
                return true;
            }

            offset += 4 + 4 + length + 4; // length + type + data + CRC
        }

        return false;
    }
}
