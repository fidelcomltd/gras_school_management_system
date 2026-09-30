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

    /// <summary>Below one 8-bit step a pixel encodes as transparent anyway, so it must not count as ink or widen the trim.</summary>
    private const float MinAlpha = 1.5f / 255f;

    /// <summary>
    /// A region whose brightest pixel is under this share of the paper's is not paper at all (the desk or floor round a
    /// photographed sheet): wholly transparent, and ink touching it is the paper's shadowed edge, not a stroke.
    /// </summary>
    private const float OffPaperRatio = 0.55f;

    /// <summary>Ink blobs smaller than this, in pixels, are dust or sensor noise.</summary>
    private const int MinComponentPixels = 6;

    /// <summary>
    /// The cleaned signature, as an unpremultiplied RGBA bitmap the caller owns; <see langword="null"/> when no strokes
    /// were found (a blank page, a solid colour, or a photograph with no contrast).
    /// </summary>
    public static SKBitmap? Clean(SKBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var (width, height) = SkiaImages.FitWithin(source.Width, source.Height, MaxLongEdge);

        using var flat = SkiaImages.FlattenScaled(source, width, height);

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

        var (paper, offPaper) = PaperEstimate(luminance, width, height);
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
        var inkDarkness = Math.Max(high, MedianAtLeast(darkness, high));

        var alpha = new float[darkness.Length];
        for (var index = 0; index < alpha.Length; index++)
        {
            if (offPaper[index])
            {
                continue;
            }

            var ramp = Math.Clamp((darkness[index] - low) / (high - low), 0f, 1f);
            var gate = ramp * ramp * (3f - (2f * ramp)); // smoothstep: what counts as ink at all
            var value = gate * Math.Min(1f, darkness[index] / inkDarkness);
            alpha[index] = value >= MinAlpha ? value : 0f;
        }

        RemoveNonStrokes(alpha, offPaper, width, height);

        int left = width, top = height, right = -1, bottom = -1, solid = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = alpha[(y * width) + x];
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

    /// <summary>
    /// Per-pixel paper brightness (block maxima, dilated, smoothed, then sampled bilinearly), and which pixels lie in a
    /// region that is not paper: a block whose dilated maximum is under <see cref="OffPaperRatio"/> of the paper level
    /// (the 90th percentile of block maxima). Dilated first, so a thick stroke filling a block never reads as desk.
    /// </summary>
    private static (float[] Paper, bool[] OffPaper) PaperEstimate(float[] luminance, int width, int height)
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

        var ranked = (float[])dilated.Clone();
        Array.Sort(ranked);
        var paperLevel = ranked[(int)(ranked.Length * 0.9)];
        var offPaper = new bool[width * height];

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
                offPaper[(y * width) + x] = dilated[((y / block) * gridWidth) + (x / block)] < OffPaperRatio * paperLevel;
            }
        }

        return (paper, offPaper);
    }

    /// <summary>
    /// Clears every ink blob (8-connected) that touches a region which is not paper, since that is the sheet's shadowed
    /// edge against the desk, or that is smaller than <see cref="MinComponentPixels"/>.
    /// </summary>
    private static void RemoveNonStrokes(float[] alpha, bool[] offPaper, int width, int height)
    {
        var visited = new bool[alpha.Length];
        var stack = new Stack<int>();
        var blob = new List<int>();
        for (var seed = 0; seed < alpha.Length; seed++)
        {
            if (visited[seed] || alpha[seed] <= 0f)
            {
                continue;
            }

            blob.Clear();
            var touchesOffPaper = false;
            visited[seed] = true;
            stack.Push(seed);
            while (stack.Count > 0)
            {
                var index = stack.Pop();
                blob.Add(index);
                int x = index % width, y = index / width;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= width || ny >= height)
                        {
                            continue;
                        }

                        var next = (ny * width) + nx;
                        touchesOffPaper |= offPaper[next];
                        if (!visited[next] && alpha[next] > 0f)
                        {
                            visited[next] = true;
                            stack.Push(next);
                        }
                    }
                }
            }

            if (touchesOffPaper || blob.Count < MinComponentPixels)
            {
                foreach (var index in blob)
                {
                    alpha[index] = 0f;
                }
            }
        }
    }

    /// <summary>The median of the values at or above <paramref name="floor"/>, from a 256-bin histogram (no sort, no copy).</summary>
    private static float MedianAtLeast(float[] values, float floor)
    {
        const int Bins = 256;
        var histogram = new int[Bins];
        var count = 0;
        foreach (var value in values)
        {
            if (value >= floor)
            {
                histogram[Math.Min(Bins - 1, (int)(value * (Bins - 1)))]++;
                count++;
            }
        }

        var seen = 0;
        for (var bin = 0; bin < Bins; bin++)
        {
            seen += histogram[bin];
            if (seen * 2 > count)
            {
                return (bin + 0.5f) / (Bins - 1);
            }
        }

        return floor;
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
