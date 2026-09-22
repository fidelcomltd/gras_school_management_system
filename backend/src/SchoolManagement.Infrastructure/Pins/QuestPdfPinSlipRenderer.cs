using System.Globalization;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Abstractions.Pins;

namespace SchoolManagement.Infrastructure.Pins;

/// <summary>The public portal's address, printed on every slip. Set <c>Portal__PublicUrl</c> on the VPS.</summary>
internal sealed class PortalOptions
{
    public const string SectionName = "Portal";

    /// <summary>e.g. <c>results.goldenroyalark.sch.ng</c>. Unset prints a generic line instead.</summary>
    public string? PublicUrl { get; set; }
}

/// <summary>
/// Spec 6.8.9 steps 6 and 7 with QuestPDF (Community licence, human ruling 2026-09-22). Slips carry no pupil name and no
/// registration number, because pins have no pupil.
/// </summary>
internal sealed class QuestPdfPinSlipRenderer : IPinSlipRenderer
{
    private const int SlipsPerPage = 4;

    private static readonly string[] DistributionColumns = ["No.", "Pin", "Pupil name", "Class", "Guardian name", "Signature"];

    private readonly string _portalLine;

    static QuestPdfPinSlipRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public QuestPdfPinSlipRenderer(IOptions<PortalOptions> portal)
    {
        ArgumentNullException.ThrowIfNull(portal);
        _portalLine = string.IsNullOrWhiteSpace(portal.Value.PublicUrl)
            ? "Check results on the school's result-checking website."
            : $"Check results at {portal.Value.PublicUrl.Trim()}";
    }

    public byte[] RenderSlips(PinSlipSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var logo = sheet.Logo.IsEmpty ? null : sheet.Logo.ToArray();

        return Document.Create(document =>
        {
            foreach (var pageSlips in sheet.Slips.Chunk(SlipsPerPage))
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24);
                    page.DefaultTextStyle(style => style.FontSize(10));
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        foreach (var slip in pageSlips)
                        {
                            table.Cell()
                                .Height(380)
                                .Border(0.75f)
                                .BorderColor(Colors.Grey.Lighten1)
                                .Padding(16)
                                .Element(cell => Slip(cell, sheet, slip, logo));
                        }
                    });
                });
            }
        }).GeneratePdf();
    }

    public byte[] RenderDistributionList(DistributionSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(style => style.FontSize(9));
            page.Header().Column(column =>
            {
                column.Item().Text(sheet.SchoolShortName).Bold().FontSize(13);
                column.Item().Text($"Pin distribution list: {sheet.BatchName} ({sheet.SessionName})");
                column.Item().PaddingBottom(8).Text("Fill in as each slip is handed over. This sheet is the school's only record of who received which pin.")
                    .Italic().FontColor(Colors.Grey.Darken1);
            });
            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.ConstantColumn(50);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    foreach (var title in DistributionColumns)
                    {
                        header.Cell().BorderBottom(1).PaddingVertical(4).Text(title).Bold();
                    }
                });

                for (var index = 0; index < sheet.Prefixes.Count; index++)
                {
                    table.Cell().Element(Row).Text((index + 1).ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(Row).Text(sheet.Prefixes[index] + "…").Bold();
                    table.Cell().Element(Row);
                    table.Cell().Element(Row);
                    table.Cell().Element(Row);
                    table.Cell().Element(Row);
                }
            });
            page.Footer().AlignRight().Text(text =>
            {
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        })).GeneratePdf();

        static IContainer Row(IContainer container) =>
            container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Height(22).AlignMiddle();
    }

    private void Slip(IContainer cell, PinSlipSheet sheet, PinSlip slip, byte[]? logo) =>
        cell.Column(column =>
        {
            column.Spacing(8);
            column.Item().Row(row =>
            {
                if (logo is not null)
                {
                    row.ConstantItem(44).Height(44).Image(logo).FitArea();
                    row.ConstantItem(10);
                }

                row.RelativeItem().AlignMiddle().Column(title =>
                {
                    title.Item().Text(sheet.SchoolShortName).Bold().FontSize(13);
                    title.Item().Text("Result checking pin").FontColor(Colors.Grey.Darken2);
                });
            });

            column.Item().PaddingTop(10).AlignCenter().Text(slip.FormattedPin).Bold().FontSize(22).LetterSpacing(0.12f);
            column.Item().AlignCenter().Text($"Valid for the {sheet.SessionName} session").FontColor(Colors.Grey.Darken2);

            column.Item().PaddingTop(10).Text(
                $"This pin can be used {slip.MaxUses.ToString(CultureInfo.InvariantCulture)} {(slip.MaxUses == 1 ? "time" : "times")}. " +
                "Looking at all three terms for one pupil in one sitting counts as one use.");
            column.Item().Text("You will need the pupil's registration number.");
            column.Item().Text(_portalLine).Bold();
            column.Item().ExtendVertical().AlignBottom().Text(sheet.BatchName).FontSize(7).FontColor(Colors.Grey.Medium);
        });
}
