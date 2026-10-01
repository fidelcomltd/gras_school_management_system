using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Infrastructure.Pupils;
using SkiaSharp;

namespace SchoolManagement.UnitTests.Infrastructure.Pupils;

/// <summary>The class safeguarding sheet PDF renders with, without and with a corrupt photograph, across more than one page.</summary>
public sealed class QuestPdfSafeguardingSheetRendererTests
{
    [Fact]
    public void Render_ProducesAPdf_ForRowsWithAndWithoutPhotographs_OverSeveralPages()
    {
        var photo = Jpeg();
        var rows = Enumerable.Range(1, 40).Select(index => new SafeguardingSheetRow(
            $"PUPIL {index} Chidera",
            index switch { 1 => new byte[] { 1, 2, 3, 4 }, _ when index % 2 == 0 => photo, _ => (ReadOnlyMemory<byte>?)null },
            index % 3 == 0 ? "Groundnuts" : "None",
            "None",
            "Not asked",
            string.Empty,
            "St. Charles Borromeo, 08037776666",
            ["Ngozi Okafor (Aunt) 08059876543", "Emeka Okafor (Father) 08031234567"],
            index % 5 == 0 ? "Yes: see office" : "No")).ToList();

        var bytes = new QuestPdfSafeguardingSheetRenderer().Render(
            new SafeguardingSheetDocument("Golden Royal Ark School", "Primary 2 Gold", "2026/2027", new DateTimeOffset(2026, 10, 5, 7, 45, 0, TimeSpan.Zero), rows));

        bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8).ShouldBeTrue();
        PdfText.Lines(bytes).ShouldContain(line => line.Contains("Generated Oct 5, 2026, 8:45am WAT. 40 pupils.", StringComparison.Ordinal));
    }

    private static byte[] Jpeg()
    {
        using var bitmap = new SKBitmap(96, 96);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SteelBlue);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return data.ToArray();
    }
}
