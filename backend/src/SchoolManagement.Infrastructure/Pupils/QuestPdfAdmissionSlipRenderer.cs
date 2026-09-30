using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Common;

namespace SchoolManagement.Infrastructure.Pupils;

/// <summary>
/// <see cref="IAdmissionSlipRenderer"/> over QuestPDF (Community licence): the slip fills the top half of an A4 portrait page
/// and the lower half is left blank (spec 9.x print rules: "half A4, printed one to a page with the lower half blank, since
/// the school files them"). A letterhead with the school's logo, motto and contacts heads it, and the head teacher's name
/// and signature close it (project lead, 2026-09-30); the registration number is printed large. Long text is clamped, so
/// no school name, address or pupil name can overflow the fixed half page and fail the print.
/// </summary>
internal sealed class QuestPdfAdmissionSlipRenderer : IAdmissionSlipRenderer
{
    private const string DateFormat = "dd/MM/yyyy";

    static QuestPdfAdmissionSlipRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    /// <inheritdoc />
    public byte[] Render(AdmissionSlipDocument slip)
    {
        ArgumentNullException.ThrowIfNull(slip);

        var logo = slip.Logo.IsEmpty ? null : Image.FromBinaryData(slip.Logo.ToArray());
        var signature = slip.Signature.IsEmpty ? null : Image.FromBinaryData(slip.Signature.ToArray());

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(style => style.FontSize(10));
            // Half of A4's 842 points, less the top margin: the slip; the rest of the page stays blank.
            page.Content().Height(PageSizes.A4.Height / 2 - 32).Border(1).Padding(16).Column(column =>
            {
                column.Spacing(5);
                column.Item().Element(item => Letterhead(item, slip, logo));
                column.Item().LineHorizontal(0.75f).LineColor(Colors.Grey.Medium);
                column.Item().AlignCenter().Text("ADMISSION SLIP").Bold().FontSize(12).LetterSpacing(0.1f);
                column.Item().PaddingBottom(4).AlignCenter().Column(number =>
                {
                    number.Item().AlignCenter().Text("Registration number").FontSize(9).FontColor(Colors.Grey.Darken2);
                    number.Item().AlignCenter().Text(slip.RegistrationNumber).Bold().FontSize(22);
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

                column.Item().ExtendVertical().AlignBottom().Row(row =>
                {
                    row.RelativeItem().AlignBottom().Column(signed =>
                    {
                        if (signature is not null)
                        {
                            signed.Item().Height(40).AlignLeft().Image(signature).FitHeight();
                        }
                        else
                        {
                            signed.Item().Height(40);
                        }

                        signed.Item().Width(170).LineHorizontal(0.75f);
                        signed.Item().Text(string.IsNullOrWhiteSpace(slip.HeadTeacherName) ? "Head teacher" : slip.HeadTeacherName).Bold().FontSize(9).ClampLines(1);
                        signed.Item().Text("Head teacher").FontSize(8).FontColor(Colors.Grey.Darken2);
                    });
                    row.RelativeItem().AlignBottom().AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken2));
                        text.Span($"Printed {PrintedTime.Format(slip.PrintedAtLagos)} by {slip.PrintedBy}.");
                    });
                });
            });
        })).GeneratePdf();
    }

    /// <summary>The logo beside the school's name, motto, address and contact line, centred; a blank column balances the logo.</summary>
    private static void Letterhead(IContainer container, AdmissionSlipDocument slip, Image? logo) =>
        container.Row(row =>
        {
            if (logo is not null)
            {
                row.ConstantItem(70).Height(70).Image(logo).FitArea();
                row.ConstantItem(6);
            }

            row.RelativeItem().AlignMiddle().Column(block =>
            {
                block.Item().AlignCenter().Text(slip.SchoolName.ToUpperInvariant()).Bold().FontSize(14).ClampLines(2);
                if (!string.IsNullOrWhiteSpace(slip.SchoolMotto))
                {
                    block.Item().AlignCenter().Text(slip.SchoolMotto).Italic().FontSize(9).ClampLines(1);
                }

                block.Item().AlignCenter().Text(slip.SchoolAddress.ReplaceLineEndings(", ")).FontSize(8.5f).FontColor(Colors.Grey.Darken2).ClampLines(2);
                if (!string.IsNullOrWhiteSpace(slip.SchoolContact))
                {
                    block.Item().AlignCenter().Text(slip.SchoolContact).FontSize(8.5f).FontColor(Colors.Grey.Darken2).ClampLines(1);
                }
            });

            if (logo is not null)
            {
                row.ConstantItem(76);
            }
        });

    private static void Row(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(3).Text(label).FontColor(Colors.Grey.Darken2);
        table.Cell().PaddingVertical(3).Text(value).Bold().ClampLines(2);
    }
}
