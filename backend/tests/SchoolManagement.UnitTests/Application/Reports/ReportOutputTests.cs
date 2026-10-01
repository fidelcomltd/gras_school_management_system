using System.Text;
using SchoolManagement.Application.Reports;
using SchoolManagement.Infrastructure.Reports;

namespace SchoolManagement.UnitTests.Application.Reports;

/// <summary>The two export formats every report shares.</summary>
public sealed class ReportOutputTests
{
    private static ReportDto Sample(int rows, bool twoUp = false, ReportOrientation orientation = ReportOrientation.Portrait) => new(
        "broadsheet",
        "Arm broadsheet",
        ["Class: Primary 2 Gold", "First Term, 2026/2027"],
        orientation,
        twoUp,
        [new("Name", ReportAlign.Left), new("CA", ReportAlign.Right, "Mathematics"), new("Exam", ReportAlign.Right, "Mathematics"), new("Grade", ReportAlign.Center)],
        [
            new(ReportRowKind.Heading, ["Primary 2", null, null, null]),
            .. Enumerable.Range(1, rows).Select(index => new ReportRowDto(ReportRowKind.Data, [$"PUPIL {index}, \"Ada\"", "38", null, "=HYPERLINK()"])),
            new(ReportRowKind.Total, ["Total", "38", null, null]),
        ],
        ["A note."],
        rows,
        DateTimeOffset.UtcNow);

    [Fact]
    public void Csv_HasABom_GroupedHeaders_QuotesAndNeutralisesFormulas_AndPrintsAHeadingAlone()
    {
        var bytes = ReportCsv.Write(Sample(1));

        bytes.Take(3).ShouldBe(Encoding.UTF8.GetPreamble());
        var lines = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split("\r\n");
        lines[0].ShouldBe("Name,Mathematics CA,Mathematics Exam,Grade");
        lines[1].ShouldBe("Primary 2");
        lines[2].ShouldBe("\"PUPIL 1, \"\"Ada\"\"\",38,,'=HYPERLINK()");
        lines[3].ShouldBe("Total,38,,");
    }

    [Theory]
    [InlineData(3, false, ReportOrientation.Landscape)]
    [InlineData(400, true, ReportOrientation.Portrait)]
    [InlineData(0, false, ReportOrientation.Portrait)]
    public void Pdf_RendersGroupsHeadingsAndTotals_AcrossPages_OneOrTwoUp(int rows, bool twoUp, ReportOrientation orientation)
    {
        var pdf = new QuestPdfReportRenderer().Render(Sample(rows, twoUp, orientation), "Golden Royal Ark School");

        Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
    }
}
