using System.Text;

namespace SchoolManagement.Application.Reports;

/// <summary>
/// A report as RFC 4180 CSV in UTF-8 with a byte-order mark (so Excel opens naira signs and accents correctly). One header
/// line, the column group prefixed to its label ("Mathematics CA"); a heading row is its text alone.
/// </summary>
public static class ReportCsv
{
    /// <summary>The bytes.</summary>
    public static byte[] Write(ReportDto report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var builder = new StringBuilder();
        Line(builder, report.Columns.Select(column => column.Group is null ? column.Label : $"{column.Group} {column.Label}"));
        foreach (var row in report.Rows)
        {
            Line(builder, row.Kind == ReportRowKind.Heading ? [(row.Cells.Count > 0 ? row.Cells[0] : null) ?? string.Empty] : row.Cells.Select(cell => cell ?? string.Empty));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    private static void Line(StringBuilder builder, IEnumerable<string> fields) =>
        builder.Append(string.Join(',', fields.Select(field => Escape(Neutralize(field))))).Append("\r\n");

    // TASK-0053's rule, as the audit export: a leading = + - @ TAB or CR is a formula to a spreadsheet, so it gets an
    // apostrophe. Report cells carry no signed numbers; a blank is written as empty, never "-".
    private static string Neutralize(string field) =>
        field.Length > 0 && field[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + field : field;

    private static string Escape(string field) =>
        field.IndexOfAny([',', '"', '\r', '\n']) < 0 ? field : $"\"{field.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
