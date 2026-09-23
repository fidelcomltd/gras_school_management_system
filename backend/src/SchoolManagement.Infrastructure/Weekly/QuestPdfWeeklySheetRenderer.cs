using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Abstractions.Weekly;
using SchoolManagement.Domain.Weekly;

namespace SchoolManagement.Infrastructure.Weekly;

/// <summary>
/// The A4 weekly report sheet (appendix G): five bordered day panels, eight fixed lines each, every line printed whether
/// filled or not so a parent can write on it. Monochrome-safe: borders carry the structure, no colour is load-bearing.
/// </summary>
internal sealed class QuestPdfWeeklySheetRenderer : IWeeklySheetPdfRenderer
{
    private const float Margin = 34; // 12 mm

    private static readonly (WeeklyField Field, string Label)[] Lines =
    [
        (WeeklyField.Behaviour, "Behaviour"),
        (WeeklyField.Performance, "Performance"),
        (WeeklyField.Dressing, "Dressing"),
        (WeeklyField.HomeWork, "Home Work"),
        (WeeklyField.Eating, "Eating"),
        (WeeklyField.SymptomsOfIllness, "Symptoms of illness"),
        (WeeklyField.TeacherComment, "Teacher's Comment"),
        (WeeklyField.ParentComment, "Parent's Comment"),
    ];

    static QuestPdfWeeklySheetRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    /// <inheritdoc />
    public byte[] Render(WeeklySheet sheet, ReadOnlyMemory<byte> logo, DateTimeOffset printedAt)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var logoBytes = logo.IsEmpty ? null : logo.ToArray();

        return Document.Create(document => document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(Margin);
                page.DefaultTextStyle(style => style.FontSize(8));
                page.Header().Element(header => Header(header, sheet, logoBytes));
                page.Content().PaddingTop(6).Column(column =>
                {
                    column.Spacing(5);
                    foreach (var day in sheet.Days)
                    {
                        column.Item().ShowEntire().Element(panel => Panel(panel, day, sheet.WeekNumber));
                    }
                });
                page.Footer().AlignRight().Text(string.Create(
                    CultureInfo.InvariantCulture, $"Printed {printedAt.ToOffset(TimeSpan.FromHours(1)):dd/MM/yyyy HH:mm}")).FontSize(7);
            }))
            .WithMetadata(new DocumentMetadata
            {
                Title = string.Create(CultureInfo.InvariantCulture, $"Weekly report, week {sheet.WeekNumber}, {sheet.TermName} {sheet.SessionName}"),
                Author = sheet.SchoolName,
                Creator = sheet.SchoolName,
            })
            .WithSettings(new DocumentSettings { ImageRasterDpi = 150, ImageCompressionQuality = ImageCompressionQuality.Medium })
            .GeneratePdf();
    }

    private static void Header(IContainer container, WeeklySheet sheet, byte[]? logo) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                if (logo is not null)
                {
                    row.ConstantItem(32).Height(32).Image(logo).FitArea();
                    row.ConstantItem(6);
                }

                row.RelativeItem().AlignMiddle().Text(sheet.SchoolName).FontSize(11).SemiBold();
            });
            column.Item().PaddingTop(4).AlignCenter().Text("WEEKLY REPORT SHEET").FontSize(13).Bold();
            column.Item().AlignCenter().Text($"{sheet.PupilName}  ·  {sheet.ClassName}").FontSize(10);
            column.Item().AlignCenter().Text(string.Create(CultureInfo.InvariantCulture,
                $"Week {sheet.WeekNumber}: {sheet.StartDate:dd/MM/yyyy} to {sheet.EndDate:dd/MM/yyyy}  ·  {sheet.TermName}, {sheet.SessionName}"));
        });

    private static void Panel(IContainer container, WeeklyDaySnapshot day, int weekNumber) =>
        container.Border(1).Padding(5).Column(column =>
        {
            column.Item().AlignCenter().Text(day.Day.ToString()).SemiBold().FontSize(9);
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(string.Create(CultureInfo.InvariantCulture, $"Date: {day.Date:dd/MM/yyyy}"));
                row.RelativeItem().AlignRight().Text(string.Create(CultureInfo.InvariantCulture, $"Week {weekNumber}"));
            });
            foreach (var (field, label) in Lines)
            {
                // G.4 rule 3: long notes wrap within the panel rather than being clipped.
                column.Item().BorderBottom(0.5f).PaddingTop(2).Row(row =>
                {
                    row.ConstantItem(92).Text($"{label}:").SemiBold();
                    row.RelativeItem().Text(day.Get(field) ?? string.Empty);
                });
            }
        });
}
