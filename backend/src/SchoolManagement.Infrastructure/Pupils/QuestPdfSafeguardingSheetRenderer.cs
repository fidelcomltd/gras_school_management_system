using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Abstractions.Pupils;

namespace SchoolManagement.Infrastructure.Pupils;

/// <summary>
/// <see cref="ISafeguardingSheetRenderer"/> over QuestPDF (Community licence, as for result sheets and pin slips): A4
/// landscape, monochrome-readable, one row per pupil with the thumbnail, the health answers, the hospital, the pickup list
/// and the barred marker. Every page repeats the header and says what the sheet is, since it is printed and carried.
/// </summary>
internal sealed class QuestPdfSafeguardingSheetRenderer : ISafeguardingSheetRenderer
{
    private static readonly string[] Columns =
        ["Photo", "Pupil", "Allergies", "Medical conditions", "Medication", "Special instructions", "Hospital", "Authorised pickup", "Barred"];

    static QuestPdfSafeguardingSheetRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    /// <inheritdoc />
    public byte[] Render(SafeguardingSheetDocument sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(style => style.FontSize(8));
            page.Header().PaddingBottom(6).Column(column =>
            {
                column.Item().Text(sheet.SchoolName).Bold().FontSize(12);
                column.Item().Text($"Class safeguarding sheet: {sheet.ArmName} ({sheet.SessionName})").FontSize(10);
                column.Item().Text(
                        "CONFIDENTIAL. Health and collection data for staff on duty only. Keep with the class; return to the office " +
                        "or shred after use.")
                    .Italic().FontColor(Colors.Grey.Darken2);
            });

            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(44);
                    columns.RelativeColumn(2.2f);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.ConstantColumn(52);
                });

                table.Header(header =>
                {
                    foreach (var title in Columns)
                    {
                        header.Cell().BorderBottom(1).PaddingVertical(3).PaddingRight(3).Text(title).Bold();
                    }
                });

                foreach (var row in sheet.Rows)
                {
                    var photo = table.Cell().Element(Cell).Height(40).Width(40);
                    if (row.Photo is { Length: > 0 } bytes)
                    {
                        photo.Image(bytes.ToArray()).FitArea();
                    }
                    else
                    {
                        photo.Border(0.5f).BorderColor(Colors.Grey.Lighten1).AlignCenter().AlignMiddle().Text("No photo").FontSize(6);
                    }

                    table.Cell().Element(Cell).Text(row.Name).Bold();
                    table.Cell().Element(Cell).Text(row.Allergies);
                    table.Cell().Element(Cell).Text(row.MedicalConditions);
                    table.Cell().Element(Cell).Text(row.Medication);
                    table.Cell().Element(Cell).Text(row.SpecialInstructions);
                    table.Cell().Element(Cell).Text(row.Hospital);
                    table.Cell().Element(Cell).Text(string.Join("\n", row.PickupPersons));
                    table.Cell().Element(Cell).Text(row.BarredMarker).Bold();
                }
            });

            page.Footer().Row(footer =>
            {
                footer.RelativeItem().Text(text =>
                {
                    text.Span("Generated ");
                    text.Span(sheet.GeneratedAtLagos.ToString("d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture));
                    text.Span($" (Lagos). {sheet.Rows.Count} pupil{(sheet.Rows.Count == 1 ? string.Empty : "s")}.");
                });
                footer.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static IContainer Cell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3).PaddingRight(3);
}
