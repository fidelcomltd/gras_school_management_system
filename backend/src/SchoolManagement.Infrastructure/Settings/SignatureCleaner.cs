using SkiaSharp;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// Lifts the pen strokes off a photographed or scanned signature (project lead, 2026-09-30): the paper becomes
/// transparent, the ink keeps its colour, and the image is trimmed to the strokes, so it sits cleanly on any printed
/// document whatever the paper, lighting or shadow in the original.
/// </summary>
/// <remarks>
/// <para>
/// 1. <b>Flatten and bound.</b> Composited over white (a transparent PNG's clear areas read as paper) and scaled down to
/// <see cref="MaxLongEdge"/>: a signature prints about 28 points high, so more pixels only cost time and storage.
/// </para>
/// <para>
/// 2. <b>Estimate the paper, locally.</b> A phone photograph is never evenly lit, so one global cut-off either eats the
/// ink in the bright corner or keeps the shadow in the dark one. The paper's brightness is estimated per region: the
/// brightest pixel of each block (a stroke never fills a whole block), spread to the neighbouring blocks so a thick
/// stroke crossing a block cannot darken the estimate, smoothed, and sampled bilinearly. Dividing by it ("flat-field
/// correction") turns every pixel into how much darker than ITS OWN paper it is: 0 for paper anywhere, whatever the
/// shadow.
/// </para>
/// <para>
/// 3. <b>Separate ink from paper.</b> Otsu's method picks the cut-off between the two populations of that darkness,
/// clamped so that sensor noise on blank paper is never promoted to ink. A soft ramp either side of it becomes the
/// gate on what counts as ink; within it, opacity is the pixel's darkness as a share of solid ink's, which keeps the
/// strokes' anti-aliased edges rather than cutting them jagged.
/// </para>
/// <para>
/// 4. <b>Recover the ink's colour.</b> An edge pixel is part ink, part paper; un-blending it from white with its alpha
/// gives the ink's own colour, so a blue signature stays blue without a pale halo.
/// </para>
/// </remarks>
internal static class SignatureCleaner
{
    /// <summary>The long edge a cleaned signature is scaled down to, when larger.</summary>
    public const int MaxLongEdge = 1200;

    /// <summary>Transparent padding kept round the trimmed strokes, in pixels.</summary>
    private const int Margin = 6;

    /// <summary>Paper-noise floor: a pixel at most this much darker than its paper is never ink.</summary>
    private const float MinThreshold = 0.12f;

    private const float MaxThreshold = 0.6f;

    /// <summary>
    /// The cleaned signature, as an unpremultiplied RGBA bitmap the caller owns; <see langword="null"/> when no strokes
    /// were found (a blank page, a solid colour, or a photograph with no contrast).
    /// </summary>
    public static SKBitmap? Clean(SKBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var scale = Math.Min(1.0, (double)MaxLongEdge / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var flat = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(flat))
        using (var image = SKImage.FromBitmap(source))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawImage(image, SKRect.Create(width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        var pixels = flat.GetPixelSpan();
        var rowBytes = flat.RowBytes;
        var luminance = new float[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = (y * rowBytes) + (x * 4);
                luminance[(y * width) + x] = ((0.299f * pixels[at]) + (0.587f * pixels[at + 1]) + (0.114f * pixels[at + 2])) / 255f;
            }
        }

        var paper = PaperEstimate(luminance, width, height);
        var darkness = new float[luminance.Length];
        for (var index = 0; index < luminance.Length; index++)
        {
            darkness[index] = Math.Clamp(1f - (luminance[index] / Math.Max(paper[index], 0.05f)), 0f, 1f);
        }

        var threshold = Math.Clamp(Otsu(darkness), MinThreshold, MaxThreshold);
        var low = threshold * 0.6f;
        var high = Math.Min(1f, threshold * 1.4f);

        // How dark solid ink is here: the median over pixels well past the cut-off. Opacity is a pixel's darkness as a
        // share of that, so an edge pixel that is a third ink is a third opaque, and un-blends to the ink's own colour
        // rather than leaving a pale fringe on coloured paper.
        var solidInk = darkness.Where(value => value >= high).OrderBy(value => value).ToArray();
        var inkDarkness = solidInk.Length == 0 ? high : Math.Max(high, solidInk[solidInk.Length / 2]);

        var alpha = new float[darkness.Length];
        int left = width, top = height, right = -1, bottom = -1, solid = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = (y * width) + x;
                var ramp = Math.Clamp((darkness[index] - low) / (high - low), 0f, 1f);
                var gate = ramp * ramp * (3f - (2f * ramp)); // smoothstep: what counts as ink at all
                var value = gate * Math.Min(1f, darkness[index] / inkDarkness);
                alpha[index] = value;
                if (value <= 0f)
                {
                    continue;
                }

                if (value >= 0.5f)
                {
                    solid++;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        // A few stray dark pixels are dust, not a signature.
        if (solid < Math.Max(20, luminance.Length / 5000))
        {
            return null;
        }

        left = Math.Max(0, left - Margin);
        top = Math.Max(0, top - Margin);
        right = Math.Min(width - 1, right + Margin);
        bottom = Math.Min(height - 1, bottom + Margin);
        var outWidth = right - left + 1;
        var outHeight = bottom - top + 1;

        var cleaned = new SKBitmap(new SKImageInfo(outWidth, outHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var output = cleaned.GetPixelSpan();
        var outRowBytes = cleaned.RowBytes;
        for (var y = 0; y < outHeight; y++)
        {
            for (var x = 0; x < outWidth; x++)
            {
                var sourceIndex = ((y + top) * width) + x + left;
                var at = ((y + top) * rowBytes) + ((x + left) * 4);
                var to = (y * outRowBytes) + (x * 4);
                var a = alpha[sourceIndex];
                if (a <= 0f)
                {
                    output[to] = output[to + 1] = output[to + 2] = output[to + 3] = 0;
                    continue;
                }

                var paperLevel = Math.Max(paper[sourceIndex], 0.05f);
                for (var channel = 0; channel < 3; channel++)
                {
                    // Corrected to white paper, then un-blended from white: observed = a * ink + (1 - a) * 1.
                    var corrected = Math.Min(1f, pixels[at + channel] / 255f / paperLevel);
                    var ink = Math.Clamp((corrected - (1f - a)) / a, 0f, 1f);
                    output[to + channel] = (byte)Math.Round(ink * 255f);
                }

                output[to + 3] = (byte)Math.Round(a * 255f);
            }
        }

        return cleaned;
    }

    /// <summary>Per-pixel paper brightness: block maxima, dilated, smoothed, then sampled bilinearly.</summary>
    private static float[] PaperEstimate(float[] luminance, int width, int height)
    {
        var block = Math.Max(4, Math.Max(width, height) / 40);
        var gridWidth = (width + block - 1) / block;
        var gridHeight = (height + block - 1) / block;

        var maxima = new float[gridWidth * gridHeight];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cell = ((y / block) * gridWidth) + (x / block);
                maxima[cell] = Math.Max(maxima[cell], luminance[(y * width) + x]);
            }
        }

        var dilated = Neighbourhood(maxima, gridWidth, gridHeight, (sum, count, max) => max);
        var smoothed = Neighbourhood(dilated, gridWidth, gridHeight, (sum, count, max) => sum / count);

        var paper = new float[width * height];
        for (var y = 0; y < height; y++)
        {
            var gy = Math.Clamp(((y + 0.5f) / block) - 0.5f, 0f, gridHeight - 1);
            var y0 = (int)gy;
            var y1 = Math.Min(y0 + 1, gridHeight - 1);
            var fy = gy - y0;
            for (var x = 0; x < width; x++)
            {
                var gx = Math.Clamp(((x + 0.5f) / block) - 0.5f, 0f, gridWidth - 1);
                var x0 = (int)gx;
                var x1 = Math.Min(x0 + 1, gridWidth - 1);
                var fx = gx - x0;
                var upper = (smoothed[(y0 * gridWidth) + x0] * (1 - fx)) + (smoothed[(y0 * gridWidth) + x1] * fx);
                var lower = (smoothed[(y1 * gridWidth) + x0] * (1 - fx)) + (smoothed[(y1 * gridWidth) + x1] * fx);
                paper[(y * width) + x] = (upper * (1 - fy)) + (lower * fy);
            }
        }

        return paper;
    }

    /// <summary>Applies <paramref name="reduce"/> over each cell's 3 by 3 neighbourhood (clipped at the edges).</summary>
    private static float[] Neighbourhood(float[] grid, int width, int height, Func<float, int, float, float> reduce)
    {
        var result = new float[grid.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                float sum = 0, max = 0;
                var count = 0;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var ny = y + dy;
                        var nx = x + dx;
                        if (ny < 0 || ny >= height || nx < 0 || nx >= width)
                        {
                            continue;
                        }

                        var value = grid[(ny * width) + nx];
                        sum += value;
                        max = Math.Max(max, value);
                        count++;
                    }
                }

                result[(y * width) + x] = reduce(sum, count, max);
            }
        }

        return result;
    }

    /// <summary>Otsu's threshold over values in [0, 1]: the cut maximising the between-class variance.</summary>
    private static float Otsu(float[] values)
    {
        const int Bins = 256;
        var histogram = new int[Bins];
        foreach (var value in values)
        {
            histogram[Math.Min(Bins - 1, (int)(value * (Bins - 1)))]++;
        }

        double total = values.Length;
        double sumAll = 0;
        for (var bin = 0; bin < Bins; bin++)
        {
            sumAll += bin * (double)histogram[bin];
        }

        double sumBelow = 0, weightBelow = 0, best = -1;
        var bestBin = 0;
        for (var bin = 0; bin < Bins; bin++)
        {
            weightBelow += histogram[bin];
            if (weightBelow == 0)
            {
                continue;
            }

            var weightAbove = total - weightBelow;
            if (weightAbove == 0)
            {
                break;
            }

            sumBelow += bin * (double)histogram[bin];
            var meanBelow = sumBelow / weightBelow;
            var meanAbove = (sumAll - sumBelow) / weightAbove;
            var between = weightBelow * weightAbove * (meanBelow - meanAbove) * (meanBelow - meanAbove);
            if (between > best)
            {
                best = between;
                bestBin = bin;
            }
        }

        return (bestBin + 0.5f) / (Bins - 1);
    }
}
