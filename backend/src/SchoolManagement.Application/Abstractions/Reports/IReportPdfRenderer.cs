using SchoolManagement.Application.Reports;

namespace SchoolManagement.Application.Abstractions.Reports;

/// <summary>Renders any report as an A4 PDF: school name, title, filters, the table, notes, and a printed-at footer.</summary>
public interface IReportPdfRenderer
{
    /// <summary>The PDF bytes.</summary>
    byte[] Render(ReportDto report, string schoolName);
}
