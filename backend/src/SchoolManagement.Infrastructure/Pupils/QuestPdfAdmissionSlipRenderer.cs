using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Abstractions.Pupils;

namespace SchoolManagement.Infrastructure.Pupils;

/// <summary>
/// <see cref="IAdmissionSlipRenderer"/> over QuestPDF (Community licence): the slip fills the top half of an A4 portrait page
/// and the lower half is left blank (spec 9.x print rules: "half A4, printed one to a page with the lower half blank, since
/// the school files them"). Monochrome; the registration number is printed large. Long text is clamped to two lines, so no
/// school name, address or pupil name can overflow the fixed half page and fail the print.
/// </summary>
internal sealed class QuestPdfAdmissionSlipRenderer : IAdmissionSlipRenderer
{
    private const string DateFormat = "dd/MM/yyyy";

    static QuestPdfAdmissionSlipRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    /// <inheritdoc />
    public byte[] Render(AdmissionSlipDocument slip)
    {
        ArgumentNullException.ThrowIfNull(slip);

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(style => style.FontSize(10));
            // Half of A4's 842 points, less the top margin: the slip; the rest of the page stays blank.
            page.Content().Height(PageSizes.A4.Height / 2 - 32).Border(1).Padding(18).Column(column =>
            {
                column.Spacing(6);
                column.Item().AlignCenter().Text(slip.SchoolName).Bold().FontSize(15).ClampLines(2);
                column.Item().AlignCenter().Text(slip.SchoolAddress).FontColor(Colors.Grey.Darken2).ClampLines(2);
                column.Item().PaddingTop(4).AlignCenter().Text("ADMISSION SLIP").Bold().FontSize(12).LetterSpacing(0.1f);
                column.Item().PaddingVertical(8).AlignCenter().Column(number =>
                {
                    number.Item().AlignCenter().Text("Registration number").FontSize(9).FontColor(Colors.Grey.Darken2);
                    number.Item().AlignCenter().Text(slip.RegistrationNumber).Bold().FontSize(24);
                });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(130);
                        columns.RelativeColumn();
                    });

                    Row(table, "Pupil", slip.PupilName);
                    Row(table, "Sex", slip.Sex);
                    Row(table, "Date of birth", slip.DateOfBirth.ToString(DateFormat, CultureInfo.InvariantCulture));
                    Row(table, "Class", slip.ClassName);
                    Row(table, "Session", slip.SessionName);
                    Row(table, "Date admitted", slip.DateAdmitted.ToString(DateFormat, CultureInfo.InvariantCulture));
                });

                column.Item().ExtendVertical().AlignBottom().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken2));
                    text.Span($"Printed {slip.PrintedAtLagos.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} (Lagos) by {slip.PrintedBy}.");
                });
            });
        })).GeneratePdf();
    }

    private static void Row(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(3).Text(label).FontColor(Colors.Grey.Darken2);
        table.Cell().PaddingVertical(3).Text(value).Bold().ClampLines(2);
    }
}
