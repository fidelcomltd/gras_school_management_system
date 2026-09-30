using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Infrastructure.Pupils;
using SkiaSharp;

namespace SchoolManagement.UnitTests.Infrastructure.Pupils;

/// <summary>The admission slip renders a one-page PDF, long names included.</summary>
public sealed class QuestPdfAdmissionSlipRendererTests
{
    [Fact]
    public void Render_ProducesAPdf()
    {
        var bytes = new QuestPdfAdmissionSlipRenderer().Render(new AdmissionSlipDocument(
            "Golden Royal Ark School", "12 School Road, Awka", "OKAFOR Chidera Ngozi Adaeze", "GRA/2026/0014", "Female",
            new DateOnly(2019, 5, 3), "Primary 2 Gold", "2026/2027", new DateOnly(2026, 9, 10), new DateTime(2026, 9, 10, 10, 15, 0),
            "Chisom Maxwell"));

        bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8).ShouldBeTrue();
    }

    [Fact]
    public void Render_TheLongestAllowedTextStillFitsTheHalfPage()
    {
        // At the fields' own maximum lengths: a school name of 160, an address of 300, names of 60 each. Unclamped, these
        // overflowed the fixed half page and QuestPDF refused the layout.
        var bytes = new QuestPdfAdmissionSlipRenderer().Render(new AdmissionSlipDocument(
            new string('G', 160), new string('A', 300), $"{new string('S', 60)} {new string('F', 60)} {new string('M', 60)}", "GRA/2026/0014",
            "Female", new DateOnly(2019, 5, 3), new string('C', 120), "2026/2027", new DateOnly(2026, 9, 10), new DateTime(2026, 9, 10, 10, 15, 0),
            new string('P', 120)));

        bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8).ShouldBeTrue();
    }

    [Fact]
    public void Render_WithFullBrandingAndTheLongestText_StillFitsTheHalfPage()
    {
        // Logo, motto, contact line, head teacher and signature on top of every field at its maximum length.
        var bytes = new QuestPdfAdmissionSlipRenderer().Render(new AdmissionSlipDocument(
            new string('G', 160), new string('A', 300), $"{new string('S', 60)} {new string('F', 60)} {new string('M', 60)}", "GRA/2026/0014",
            "Female", new DateOnly(2019, 5, 3), new string('C', 120), "2026/2027", new DateOnly(2026, 9, 10), new DateTime(2026, 9, 10, 10, 15, 0),
            new string('P', 120), new string('m', 120), "+2348031234567  ·  " + new string('e', 160) + "@school.ng", new string('H', 120),
            Png(200, 190, SKColors.Goldenrod), Png(600, 180, SKColors.Navy)));

        bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8).ShouldBeTrue();
    }

    private static byte[] Png(int width, int height, SKColor colour)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(colour);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
