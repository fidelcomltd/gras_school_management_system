using System.Globalization;
using Microsoft.Extensions.Options;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Results.Sheets;
using SchoolManagement.Domain.Results;
using SchoolManagement.Infrastructure.Pins;

namespace SchoolManagement.Infrastructure.Results;

/// <summary>
/// The A4 result sheet (spec 6.9.6; Appendices C, E, F) from a <see cref="ResultSheet"/>, the same model the portal page
/// renders, so the two cannot disagree (C.8 rule 1). Monochrome-safe: borders carry the structure and the only fill is
/// a 10 per cent header tint. 8 point body, never below 7. The header repeats when a long subject list flows to page 2.
/// </summary>
internal sealed class QuestPdfResultSheetRenderer : IResultSheetPdfRenderer
{
    private const float Margin = 34; // 12 mm
    private const float QrSize = 57; // 20 mm
    private const float StampSize = 100; // 35 mm, C.6
    private static readonly string HeaderTint = Colors.Grey.Lighten4;

    private readonly string? _publicUrl;

    static QuestPdfResultSheetRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public QuestPdfResultSheetRenderer(IOptions<PortalOptions> portal)
    {
        ArgumentNullException.ThrowIfNull(portal);
        var configured = portal.Value.PublicUrl?.Trim().TrimEnd('/');
        _publicUrl = string.IsNullOrEmpty(configured) ? null
            : configured.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? configured
            : $"https://{configured}";
    }

    public Uri? VerificationUrl(string token) => _publicUrl is null ? null : new Uri($"{_publicUrl}/verify/{token}");

    public byte[] Render(ResultSheet sheet, ResultSheetPdfExtras extras)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(extras);
        var logo = extras.Logo.IsEmpty ? null : extras.Logo.ToArray();
        var signature = extras.Signature.IsEmpty ? null : extras.Signature.ToArray();
        var qr = extras.VerificationUrl is { } url ? QrPng(url.AbsoluteUri) : null;

        return Document.Create(document => document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(Margin);
                page.DefaultTextStyle(style => style.FontSize(8));
                page.Header().Element(header => Header(header, sheet, logo));
                page.Content().PaddingVertical(6).Element(content => Content(content, sheet, signature));
                page.Footer().Element(footer => Footer(footer, extras, qr));
            }))
            .WithMetadata(new DocumentMetadata { Title = $"{sheet.TermName} result, {sheet.AcademicYear}", Author = sheet.SchoolName, Creator = sheet.SchoolName })
            .WithSettings(new DocumentSettings { ImageRasterDpi = 200, ImageCompressionQuality = ImageCompressionQuality.High })
            .GeneratePdf();
    }

    private static void Header(IContainer container, ResultSheet sheet, byte[]? logo) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                if (logo is not null)
                {
                    row.ConstantItem(52).Height(52).Image(logo).FitArea();
                    row.ConstantItem(8);
                }

                row.RelativeItem().AlignMiddle().Column(title =>
                {
                    title.Item().AlignCenter().Text(sheet.SchoolName.ToUpperInvariant()).Bold().FontSize(14);
                    if (sheet.SchoolMotto is { Length: > 0 } motto)
                    {
                        title.Item().AlignCenter().Text(motto).Italic();
                    }

                    if (sheet.SchoolAddress is { Length: > 0 } address)
                    {
                        title.Item().AlignCenter().Text(address.ReplaceLineEndings(", ")).FontSize(7.5f);
                    }

                    title.Item().PaddingTop(3).AlignCenter()
                        .Text(sheet.Section == SheetSection.Nursery ? "NURSERY REPORT SHEET" : "PUPIL'S REPORT SHEET").Bold().FontSize(10);
                });

                if (logo is not null)
                {
                    row.ConstantItem(60); // balances the logo so the title stays centred
                }
            });

            column.Item().PaddingTop(6).Border(0.75f).Padding(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(62);
                    columns.RelativeColumn(3);
                    columns.ConstantColumn(78);
                    columns.RelativeColumn(2);
                });
                Field(table, "Name", sheet.PupilName, bold: true);
                Field(table, "Reg. no.", sheet.RegistrationNumber);
                Field(table, "Class", sheet.Section == SheetSection.Primary ? sheet.ClassName.Split(',')[0] : sheet.ClassName);
                Field(table, "Age", sheet.Age?.ToString(CultureInfo.InvariantCulture));
                Field(table, "Term", sheet.TermName);
                Field(table, "Session", sheet.AcademicYear);
                Field(table, "Instructor", sheet.Instructor);
                Field(table, "Next term begins", sheet.NextTermBegins?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            });

            if (sheet.RevisionNumber > 1)
            {
                column.Item().PaddingTop(3).Text($"Revision {sheet.RevisionNumber.ToString(CultureInfo.InvariantCulture)}. {sheet.RevisionNotice}").Bold();
            }
        });

    private static void Field(TableDescriptor table, string label, string? value, bool bold = false)
    {
        table.Cell().PaddingVertical(1.5f).Text(label).FontColor(Colors.Grey.Darken3);
        var text = table.Cell().PaddingVertical(1.5f).Text(value ?? string.Empty);
        if (bold)
        {
            text.Bold();
        }
    }

    private static void Content(IContainer container, ResultSheet sheet, byte[]? signature) =>
        container.Column(column =>
        {
            column.Spacing(7);
            column.Item().Element(item => Subjects(item, sheet));
            column.Item().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem().Border(0.75f).Padding(4).Column(summary =>
                {
                    summary.Item().Text(text =>
                    {
                        text.Span("Term average: ");
                        text.Span(sheet.TermAverage?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty).Bold();
                    });
                    summary.Item().Text(text =>
                    {
                        text.Span("Overall grade: ");
                        text.Span(sheet.OverallGrade ?? string.Empty).Bold();
                    });
                });

                if (sheet.Attendance is { } attendance)
                {
                    row.RelativeItem(2).Border(0.75f).Padding(4).Column(block =>
                    {
                        block.Item().Text("ATTENDANCE").Bold();
                        block.Item().Text(
                            $"Times school opened: {attendance.Opened}    Times present: {attendance.Present}    Times absent: {attendance.Absent}");
                    });
                }
            });

            foreach (var pair in sheet.RatingBlocks.Chunk(sheet.RatingBlocks.Any(block => block.HasComments) ? 1 : 2))
            {
                column.Item().Row(row =>
                {
                    row.Spacing(8);
                    foreach (var block in pair)
                    {
                        row.RelativeItem().Element(item => Ratings(item, block));
                    }

                    if (pair.Length == 1 && !pair[0].HasComments)
                    {
                        row.RelativeItem();
                    }
                });
            }

            if (sheet.RatingKey is { } key)
            {
                column.Item().Text($"Key: {key}").FontSize(7);
            }

            // C.8 rule 5: the remarks and signature block is never split or orphaned.
            column.Item().ShowEntire().Element(item => Remarks(item, sheet, signature));
            column.Item().Element(item => GradeKey(item, sheet));
        });

    private static void Subjects(IContainer container, ResultSheet sheet) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);
                foreach (var _ in sheet.Columns)
                {
                    columns.RelativeColumn(1.2f);
                }

                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1);
                columns.RelativeColumn(2.2f);
            });

            table.Header(header =>
            {
                HeadCell(header.Cell(), "SUBJECT", left: true);
                foreach (var column in sheet.Columns)
                {
                    HeadCell(header.Cell(), $"{column.Label}\n({column.MaxMark.ToString(CultureInfo.InvariantCulture)})");
                }

                HeadCell(header.Cell(), $"TOTAL\n({sheet.Columns.Sum(column => column.MaxMark).ToString(CultureInfo.InvariantCulture)})");
                HeadCell(header.Cell(), "GRADE");
                HeadCell(header.Cell(), "REMARK", left: true);
            });

            foreach (var row in sheet.Subjects)
            {
                BodyCell(table.Cell()).Text(row.Subject);
                foreach (var cell in row.Cells)
                {
                    BodyCell(table.Cell()).AlignCenter().Text(cell ?? string.Empty);
                }

                BodyCell(table.Cell()).AlignCenter().Text(row.Total?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Bold();
                BodyCell(table.Cell()).AlignCenter().Text(row.Grade ?? string.Empty);
                BodyCell(table.Cell()).Text(row.Comment ?? string.Empty);
            }

            BodyCell(table.Cell()).Text("GRAND TOTAL").Bold();
            foreach (var total in sheet.GrandTotals)
            {
                BodyCell(table.Cell()).AlignCenter().Text(total?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Bold();
            }

            BodyCell(table.Cell());
            BodyCell(table.Cell());
        });

    private static void Ratings(IContainer container, SheetRatingBlock block) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);
                foreach (var _ in block.PointCodes)
                {
                    columns.ConstantColumn(18);
                }

                if (block.HasComments)
                {
                    columns.RelativeColumn(4);
                }
            });

            table.Header(header =>
            {
                HeadCell(header.Cell(), block.Name, left: true);
                foreach (var code in block.PointCodes)
                {
                    HeadCell(header.Cell(), code);
                }

                if (block.HasComments)
                {
                    HeadCell(header.Cell(), "COMMENT", left: true);
                }
            });

            foreach (var item in block.Items)
            {
                BodyCell(table.Cell()).Text(item.Name);
                foreach (var code in block.PointCodes)
                {
                    BodyCell(table.Cell()).AlignCenter().Text(item.PointCode == code ? "X" : string.Empty).Bold();
                }

                if (block.HasComments)
                {
                    BodyCell(table.Cell()).Text(item.Comment ?? string.Empty);
                }
            }
        });

    private static void Remarks(IContainer container, ResultSheet sheet, byte[]? signature) =>
        container.Row(row =>
        {
            row.Spacing(8);
            row.RelativeItem().Column(column =>
            {
                column.Spacing(4);
                var primary = sheet.Section == SheetSection.Primary;
                Remark(column, primary ? "Teacher's report" : "Teacher's comment", sheet.TeacherComment);
                Remark(column, primary ? "Head teacher's report" : "Head teacher's comment", sheet.HeadTeacherComment);
                column.Item().Row(signed =>
                {
                    signed.RelativeItem().Column(block =>
                    {
                        if (signature is not null)
                        {
                            block.Item().Height(28).AlignLeft().Image(signature).FitHeight();
                        }
                        else
                        {
                            block.Item().Height(28).BorderBottom(0.75f);
                        }

                        block.Item().Text(sheet.HeadTeacherName ?? "Head teacher").Bold();
                        block.Item().Text("Head teacher").FontSize(7);
                    });
                    signed.ConstantItem(12);
                    signed.RelativeItem().AlignBottom().Text(text =>
                    {
                        text.Span("Date issued: ");
                        text.Span(sheet.IssuedAt is { } issued ? issued.ToOffset(TimeSpan.FromHours(1)).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty).Bold();
                    });
                });
            });

            row.ConstantItem(StampSize).Height(StampSize).Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                .AlignBottom().AlignCenter().PaddingBottom(3).Text("School stamp").FontSize(6.5f).FontColor(Colors.Grey.Medium);
        });

    private static void Remark(ColumnDescriptor column, string label, string? text) =>
        column.Item().Border(0.75f).Padding(4).MinHeight(30).Column(block =>
        {
            block.Item().Text(label).Bold();
            block.Item().Text(text ?? string.Empty);
        });

    private static void GradeKey(IContainer container, ResultSheet sheet) =>
        container.Column(column =>
        {
            column.Item().Text("GRADE KEY").Bold().FontSize(7);
            column.Item().Text(string.Join("    ", sheet.GradeKey.Select(band => $"{band.Grade}: {band.Range} {band.Word}"))).FontSize(7);
        });

    private static void Footer(IContainer container, ResultSheetPdfExtras extras, byte[]? qr) =>
        container.BorderTop(0.5f).PaddingTop(4).Row(row =>
        {
            if (qr is not null && extras.VerificationToken is { } token)
            {
                row.ConstantItem(QrSize).Column(column =>
                {
                    column.Item().Width(QrSize).Height(QrSize).Image(qr).FitArea();
                });
                row.ConstantItem(8);
                row.RelativeItem().AlignMiddle().Column(column =>
                {
                    column.Item().Text("Verify this result").Bold().FontSize(7);
                    column.Item().Text(ResultVerification.Format(token)).FontSize(9).Bold().LetterSpacing(0.05f);
                    column.Item().Text($"Scan the code, or visit {extras.VerificationUrl!.GetLeftPart(UriPartial.Authority)}/verify and type the code above.").FontSize(6.5f);
                });
            }
            else
            {
                row.RelativeItem();
            }

            row.ConstantItem(130).AlignRight().AlignBottom().Column(column =>
            {
                column.Item().AlignRight().Text($"Printed {extras.PrintedAt.ToOffset(TimeSpan.FromHours(1)).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} WAT").FontSize(6.5f);
                column.Item().AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(6.5f));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });

    private static void HeadCell(IContainer cell, string text, bool left = false)
    {
        var box = cell.Border(0.75f).Background(HeaderTint).PaddingVertical(2).PaddingHorizontal(3);
        (left ? box.AlignLeft() : box.AlignCenter()).AlignMiddle().Text(text).Bold().FontSize(7.5f);
    }

    private static IContainer BodyCell(IContainer cell) => cell.Border(0.5f).PaddingVertical(1.5f).PaddingHorizontal(3).AlignMiddle();

    private static byte[] QrPng(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(4);
    }
}
