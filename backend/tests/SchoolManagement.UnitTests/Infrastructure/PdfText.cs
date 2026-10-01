using UglyToad.PdfPig;

namespace SchoolManagement.UnitTests.Infrastructure;

/// <summary>Reads a rendered PDF back as text, so a test can check what a print says and that a phrase stays on one line.</summary>
internal static class PdfText
{
    /// <summary>Every printed line on every page: the words sharing a baseline, left to right, joined by a space.</summary>
    public static IReadOnlyList<string> Lines(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return document.GetPages()
            .SelectMany(page => page.GetWords()
                .GroupBy(word => Math.Round(word.Letters[0].StartBaseLine.Y, 1))
                .OrderByDescending(line => line.Key)
                .Select(line => string.Join(' ', line.OrderBy(word => word.BoundingBox.Left).Select(word => word.Text))))
            .ToList();
    }
}
