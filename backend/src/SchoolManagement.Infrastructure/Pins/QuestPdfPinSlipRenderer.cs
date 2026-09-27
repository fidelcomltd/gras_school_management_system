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

    /// <summary>Where generated result PDFs are cached. Unset uses the system temp directory.</summary>
    public string? PdfCacheDirectory { get; set; }
}

/// <summary>
/// Spec 6.8.9 steps 6 and 7 with QuestPDF (Community licence, human ruling 2026-09-22). Slips carry no pupil name and no
/// registration number, because pins have no pupil.
/// </summary>
internal sealed class QuestPdfPinSlipRenderer : IPinSlipRenderer
{
    private const int SlipsPerPage = 5;

    // A4's 842pt height less the margins and gaps, shared by five cards: each slip is a wide card, cut along its border.
    private const float SlipHeight = 146;

    // How a prefix is shown wherever the rest of the pin is not (spec 6.8: the system never reveals the remainder).
    private const string HiddenRest = " ••••••";

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
                    page.Content().Column(column =>
                    {
                        column.Spacing(8);
                        foreach (var slip in pageSlips)
                        {
                            column.Item()
                                .Height(SlipHeight)
                                .Border(0.75f)
                                .BorderColor(Colors.Grey.Lighten1)
                                .Padding(12)
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
                column.Item().Text("Only each pin's first four characters are shown: the full pin exists only on its printed slip.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
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
                    table.Cell().Element(Row).Text(sheet.Prefixes[index] + HiddenRest).Bold();
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
        cell.Row(row =>
        {
            // Left: whose pin this is.
            row.ConstantItem(170).Column(school =>
            {
                school.Spacing(4);
                if (logo is not null)
                {
                    school.Item().Height(40).Width(40).Image(logo).FitArea();
                }

                school.Item().Text(sheet.SchoolShortName).Bold().FontSize(12);
                school.Item().Text("Result checking pin").FontColor(Colors.Grey.Darken2);
                school.Item().Text($"Valid for the {sheet.SessionName} session").FontSize(9).FontColor(Colors.Grey.Darken2);
                school.Item().ExtendVertical().AlignBottom().Text(sheet.BatchName).FontSize(7).FontColor(Colors.Grey.Medium);
            });

            row.ConstantItem(14);
            row.ConstantItem(0.75f).Background(Colors.Grey.Lighten2);
            row.ConstantItem(14);

            // Right: the pin itself, large, then how to use it.
            row.RelativeItem().Column(pin =>
            {
                pin.Spacing(4);
                pin.Item().Text("PIN").FontSize(8).FontColor(Colors.Grey.Darken1).LetterSpacing(0.1f);
                pin.Item().Text(slip.FormattedPin).Bold().FontSize(24).LetterSpacing(0.12f);
                pin.Item().PaddingTop(4).Text(
                    $"This pin can be used {slip.MaxUses.ToString(CultureInfo.InvariantCulture)} {(slip.MaxUses == 1 ? "time" : "times")}. " +
                    "Looking at all three terms for one pupil in one sitting counts as one use.").FontSize(9);
                pin.Item().Text("You will need the pupil's registration number.").FontSize(9);
                pin.Item().Text(_portalLine).Bold().FontSize(9);
            });
        });
}
