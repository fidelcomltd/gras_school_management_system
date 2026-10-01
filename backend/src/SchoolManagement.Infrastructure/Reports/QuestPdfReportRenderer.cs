using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Reports;

namespace SchoolManagement.Infrastructure.Reports;

/// <summary>
/// Any <see cref="ReportDto"/> as an A4 PDF. Monochrome-safe (borders carry the structure, the only fill a light header tint),
/// the table header repeats on every page, rows grow to fit their text, so nothing here needs a fixed height.
/// </summary>
internal sealed class QuestPdfReportRenderer : IReportPdfRenderer
{
    private const float Margin = 28; // 10 mm
    private static readonly string Tint = Colors.Grey.Lighten4;

    static QuestPdfReportRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(ReportDto report, string schoolName)
    {
        ArgumentNullException.ThrowIfNull(report);
        var count = report.Columns.Count;
        var size = count > 40 ? 5f : count > 20 ? 5.5f : count > 14 ? 6.5f : 8f;
        var padding = count > 20 ? 1f : 3f;
        var widths = Widths(report);

        return Document.Create(document => document.Page(page =>
            {
                page.Size(report.Orientation == ReportOrientation.Landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(Margin);
                page.DefaultTextStyle(style => style.FontSize(size));
                page.Header().PaddingBottom(6).Column(column =>
                {
                    column.Item().Text(schoolName).Bold().FontSize(12);
                    column.Item().Text(report.Title).SemiBold().FontSize(10.5f);
                    if (report.Filters.Count > 0)
                    {
                        column.Item().Text(string.Join("  ·  ", report.Filters)).FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                    }
                });
                page.Content().Column(column =>
                {
                    column.Spacing(6);
                    if (report.TwoUp)
                    {
                        // Down the left column, then the right, page by page: position order reads straight through.
                        column.Item().MultiColumn(columns =>
                        {
                            columns.Columns(2);
                            columns.Spacing(10);
                            columns.Content().Element(item => Table(item, report, widths, padding));
                        });
                    }
                    else
                    {
                        column.Item().Element(item => Table(item, report, widths, padding));
                    }

                    if (report.Rows.Count == 0)
                    {
                        column.Item().Text("Nothing matches these filters.").Italic();
                    }

                    foreach (var note in report.Notes)
                    {
                        column.Item().Text(note).FontSize(7.5f);
                    }
                });
                page.Footer().BorderTop(0.5f).PaddingTop(3).Row(row =>
                {
                    row.RelativeItem().Text($"Printed {SchoolTime.Stamp(report.GeneratedAtUtc)}")
                        .FontSize(6.5f);
                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(6.5f));
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            }))
            .WithMetadata(new DocumentMetadata { Title = report.Title, Author = schoolName, Creator = schoolName })
            .GeneratePdf();
    }

    // Relative widths from the longest text in each column (its label included), bounded so one long name cannot starve
    // the numbers: a broadsheet's CA, Exam and Total columns keep room for their headings.
    private static float[] Widths(ReportDto report) =>
        report.Columns.Select((column, index) =>
            {
                var longest = report.Rows.Where(row => row.Kind != ReportRowKind.Heading && index < row.Cells.Count)
                    .Select(row => row.Cells[index]?.Length ?? 0)
                    .DefaultIfEmpty(0)
                    .Max();
                return (float)Math.Clamp(Math.Max(longest, column.Label.Length), 3, 22);
            })
            .ToArray();

    private static void Table(IContainer container, ReportDto report, float[] widths, float padding) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var width in widths)
                {
                    columns.RelativeColumn(width);
                }
            });

            table.Header(header =>
            {
                if (report.Columns.Any(column => column.Group is not null))
                {
                    for (var index = 0; index < report.Columns.Count;)
                    {
                        var group = report.Columns[index].Group;
                        var span = 1;
                        while (group is not null && index + span < report.Columns.Count && report.Columns[index + span].Group == group)
                        {
                            span++;
                        }

                        header.Cell().ColumnSpan((uint)span).Element(cell => HeadCell(cell, group ?? string.Empty, padding));
                        index += span;
                    }
                }

                foreach (var column in report.Columns)
                {
                    header.Cell().Element(cell => HeadCell(cell, column.Label, padding));
                }
            });

            foreach (var row in report.Rows)
            {
                if (row.Kind == ReportRowKind.Heading)
                {
                    table.Cell().ColumnSpan((uint)report.Columns.Count).Border(0.5f).Background(Tint).PaddingVertical(2).PaddingHorizontal(3)
                        .Text((row.Cells.Count > 0 ? row.Cells[0] : null) ?? string.Empty).Bold();
                    continue;
                }

                for (var index = 0; index < report.Columns.Count; index++)
                {
                    var text = index < row.Cells.Count ? row.Cells[index] ?? string.Empty : string.Empty;
                    var cell = table.Cell().Border(0.5f).PaddingVertical(1.5f).PaddingHorizontal(padding);
                    cell = report.Columns[index].Align switch
                    {
                        ReportAlign.Right => cell.AlignRight(),
                        ReportAlign.Center => cell.AlignCenter(),
                        _ => cell.AlignLeft(),
                    };
                    var span = cell.Text(text);
                    if (row.Kind is ReportRowKind.Subtotal or ReportRowKind.Total)
                    {
                        span.Bold();
                    }
                }
            }
        });

    private static void HeadCell(IContainer cell, string text, float padding) =>
        cell.Border(0.75f).Background(Tint).PaddingVertical(2).PaddingHorizontal(padding).AlignCenter().AlignMiddle().Text(text).Bold();
}
