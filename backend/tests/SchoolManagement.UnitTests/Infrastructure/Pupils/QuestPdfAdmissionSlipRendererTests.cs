using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Infrastructure.Pupils;

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
}
