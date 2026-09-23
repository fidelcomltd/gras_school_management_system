using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Abstractions.Pupils;

/// <summary>
/// Reads and writes the bulk-import spreadsheet (spec 6.5.13). Infrastructure implements it over an XLSX library, so the
/// parsing rules in <c>Pupils.Import</c> stay library-free and unit-testable.
/// </summary>
public interface IPupilImportWorkbook
{
    /// <summary>
    /// Reads the data sheet (the one named <c>Pupils</c>, else the first): row 1 is the header, every later row with any
    /// content is a data row. At most <paramref name="maxRows"/> data rows are materialised; <see
    /// cref="ImportSheet.DataRowCount"/> still counts them all. Fails with <c>import.file_invalid</c> when the bytes are
    /// not a readable XLSX workbook.
    /// </summary>
    Result<ImportSheet> Read(ReadOnlyMemory<byte> content, int maxRows);

    /// <summary>An XLSX workbook: the data sheet with <paramref name="dataHeaders"/> as its header row, then each reference sheet.</summary>
    byte[] WriteTemplate(IReadOnlyList<string> dataHeaders, IReadOnlyList<ImportReferenceSheet> referenceSheets);
}

/// <summary>One cell as the workbook stored it.</summary>
/// <param name="Text">The trimmed text; a number's invariant string; empty when blank.</param>
/// <param name="Number">The numeric value of a number cell (a date serial typed as a plain number lands here).</param>
/// <param name="Date">The value of a date-formatted cell.</param>
public sealed record ImportCell(string Text, double? Number, DateTime? Date)
{
    /// <summary>The blank cell.</summary>
    public static readonly ImportCell Blank = new(string.Empty, null, null);

    /// <summary>Whether nothing was entered.</summary>
    public bool IsBlank => Text.Length == 0 && Number is null && Date is null;
}

/// <summary>The data sheet.</summary>
/// <param name="Headers">Row 1, trimmed, in column order.</param>
/// <param name="Rows">Data rows in sheet order, each aligned to <paramref name="Headers"/>.</param>
/// <param name="DataRowCount">Every data row with content, including any beyond the materialised cap.</param>
public sealed record ImportSheet(IReadOnlyList<string> Headers, IReadOnlyList<ImportSheetRow> Rows, int DataRowCount);

/// <summary>One data row.</summary>
/// <param name="SheetRow">The spreadsheet's own row number (the header is row 1).</param>
/// <param name="Cells">One per header, blank where the row has nothing.</param>
public sealed record ImportSheetRow(int SheetRow, IReadOnlyList<ImportCell> Cells);

/// <summary>A reference sheet in the template, for example the accepted values.</summary>
/// <param name="Name">The tab name.</param>
/// <param name="Headers">Its header row.</param>
/// <param name="Columns">Values per column, top to bottom; columns may differ in length.</param>
public sealed record ImportReferenceSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Columns);
