namespace SchoolManagement.Application.Reports;

/// <summary>How a column's cells sit.</summary>
public enum ReportAlign
{
    /// <summary>Text.</summary>
    Left,

    /// <summary>Short codes and ticks.</summary>
    Center,

    /// <summary>Numbers.</summary>
    Right,
}

/// <summary>The PDF's page orientation.</summary>
public enum ReportOrientation
{
    /// <summary>A4 upright.</summary>
    Portrait,

    /// <summary>A4 on its side, for wide tables such as the broadsheet.</summary>
    Landscape,
}

/// <summary>What a row is, so screen, CSV and PDF can each style it.</summary>
public enum ReportRowKind
{
    /// <summary>An ordinary row.</summary>
    Data,

    /// <summary>A section heading: the first cell carries the text, the rest are null.</summary>
    Heading,

    /// <summary>A subtotal under a section.</summary>
    Subtotal,

    /// <summary>The grand total.</summary>
    Total,
}

/// <summary>One column.</summary>
/// <param name="Label">The heading.</param>
/// <param name="Align">How its cells sit.</param>
/// <param name="Group">A heading over consecutive columns sharing it (the broadsheet's subject over CA, Exam and Total), or null.</param>
public sealed record ReportColumnDto(string Label, ReportAlign Align, string? Group = null);

/// <summary>One row: one cell per column, already formatted, null for blank.</summary>
/// <param name="Kind">What the row is.</param>
/// <param name="Cells">The cells, in column order.</param>
public sealed record ReportRowDto(ReportRowKind Kind, IReadOnlyList<string?> Cells);

/// <summary>
/// Every report in spec 15 section 10 as one tabular shape (spec 15: read-only views, CSV and PDF under <c>report.export</c>),
/// so a single screen, CSV writer and PDF renderer serve all of them. Cells are formatted on the server, the same text in
/// all three outputs.
/// </summary>
/// <param name="Key">The report's route name, e.g. <c>broadsheet</c>.</param>
/// <param name="Title">The heading.</param>
/// <param name="Filters">The filters applied, as readable lines ("Class: Primary 3A").</param>
/// <param name="Orientation">The PDF's orientation.</param>
/// <param name="TwoUp">The PDF prints the table in two columns side by side (the merit list).</param>
/// <param name="Columns">The columns.</param>
/// <param name="Rows">The rows.</param>
/// <param name="Notes">Lines printed under the table.</param>
/// <param name="RowCount">Data rows, the figure every export audits.</param>
/// <param name="GeneratedAtUtc">When this copy was produced.</param>
public sealed record ReportDto(
    string Key,
    string Title,
    IReadOnlyList<string> Filters,
    ReportOrientation Orientation,
    bool TwoUp,
    IReadOnlyList<ReportColumnDto> Columns,
    IReadOnlyList<ReportRowDto> Rows,
    IReadOnlyList<string> Notes,
    int RowCount,
    DateTimeOffset GeneratedAtUtc);

/// <summary>An exported report.</summary>
/// <param name="FileName">The download name.</param>
/// <param name="ContentType"><c>text/csv</c> or <c>application/pdf</c>.</param>
/// <param name="Content">The bytes.</param>
public sealed record ReportFile(string FileName, string ContentType, ReadOnlyMemory<byte> Content);
