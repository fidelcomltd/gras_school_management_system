using SkiaSharp;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>Scaling helpers shared by <see cref="SkiaSchoolImageProcessor"/> and <see cref="SignatureCleaner"/>.</summary>
internal static class SkiaImages
{
    /// <summary>The size that fits <paramref name="maxLongEdge"/> with the aspect kept; never larger than the source.</summary>
    public static (int Width, int Height) FitWithin(int width, int height, int maxLongEdge)
    {
        var scale = Math.Min(1.0, (double)maxLongEdge / Math.Max(width, height));
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    /// <summary>
    /// <paramref name="source"/> drawn at <paramref name="width"/> by <paramref name="height"/> over white into an opaque
    /// RGBA bitmap the caller owns: a transparent pixel reads as paper, and a JPEG has no alpha to lose.
    /// </summary>
    public static SKBitmap FlattenScaled(SKBitmap source, int width, int height)
    {
        var flat = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(flat);
        using var image = SKImage.FromBitmap(source);
        canvas.Clear(SKColors.White);
        canvas.DrawImage(image, SKRect.Create(width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        return flat;
    }
}
