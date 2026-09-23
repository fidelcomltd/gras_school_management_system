using System.Globalization;
using System.IO.Compression;
using ClosedXML.Excel;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Infrastructure.Pupils;

/// <summary>
/// <see cref="IPupilImportWorkbook"/> over ClosedXML (MIT). The upload is untrusted: the zip's declared uncompressed size is
/// checked before ClosedXML inflates anything, and any failure to parse is reported as an unreadable file, never a 500.
/// </summary>
internal sealed class ClosedXmlPupilImportWorkbook : IPupilImportWorkbook
{
    /// <summary>
    /// A full 1000-row, 43-column register inflates to about 5 MB. ClosedXML holds the whole workbook in memory before
    /// any row cap applies, so this bound is what keeps one upload from costing the VPS gigabytes.
    /// </summary>
    private const long MaxUncompressedBytes = 25L * 1024 * 1024;

    /// <summary>The template has 43 columns; anything to the right of this is ignored rather than allocated per row.</summary>
    private const int MaxColumns = 128;

    private const string DataSheetName = "Pupils";

    private static readonly Error Unreadable = Error.Validation(
        "import.file_invalid", "This file could not be read as an Excel workbook. Save it as .xlsx and upload it again.");

    /// <inheritdoc />
    public Result<ImportSheet> Read(ReadOnlyMemory<byte> content, int maxRows)
    {
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        if (!WithinUncompressedLimit(stream))
        {
            return Result.Failure<ImportSheet>(Unreadable);
        }

        stream.Position = 0;

        // Justification: the bytes are an arbitrary upload and OpenXML/ClosedXML throw a wide, undocumented set of
        // exception types on malformed input (zip, XML, packaging, formula parsing). Every one means the same thing here.
#pragma warning disable CA1031 // Do not catch general exception types.
        try
        {
            using var workbook = new XLWorkbook(stream);
            return Result.Success(ReadSheet(PickDataSheet(workbook), maxRows));
        }
        catch (Exception)
        {
            return Result.Failure<ImportSheet>(Unreadable);
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public byte[] WriteTemplate(IReadOnlyList<string> dataHeaders, IReadOnlyList<ImportReferenceSheet> referenceSheets)
    {
        ArgumentNullException.ThrowIfNull(dataHeaders);
        ArgumentNullException.ThrowIfNull(referenceSheets);

        using var workbook = new XLWorkbook();
        var data = workbook.Worksheets.Add(DataSheetName);
        for (var index = 0; index < dataHeaders.Count; index++)
        {
            var header = dataHeaders[index];
            data.Cell(1, index + 1).Value = header;
            var column = data.Column(index + 1);
            column.Width = Math.Max(12, header.Length + 4);

            // Text format so a typed 0803... keeps its leading zero.
            if (header.EndsWith("Phone", StringComparison.Ordinal) || header.EndsWith("WhatsApp", StringComparison.Ordinal))
            {
                column.Style.NumberFormat.Format = "@";
            }
        }

        data.Row(1).Style.Font.Bold = true;
        data.SheetView.FreezeRows(1);

        foreach (var reference in referenceSheets)
        {
            var sheet = workbook.Worksheets.Add(reference.Name);
            for (var column = 0; column < reference.Headers.Count; column++)
            {
                sheet.Cell(1, column + 1).Value = reference.Headers[column];
                sheet.Column(column + 1).Width = Math.Max(14, reference.Headers[column].Length + 4);
                var values = column < reference.Columns.Count ? reference.Columns[column] : [];
                for (var row = 0; row < values.Count; row++)
                {
                    sheet.Cell(row + 2, column + 1).Value = values[row];
                }
            }

            sheet.Row(1).Style.Font.Bold = true;
            sheet.SheetView.FreezeRows(1);
        }

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    // The declared sizes, summed without overflow: a crafted Zip64 entry can declare close to long.MaxValue.
    private static bool WithinUncompressedLimit(Stream stream)
    {
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                if (entry.Length < 0 || entry.Length > MaxUncompressedBytes - total)
                {
                    return false;
                }

                total += entry.Length;
            }

            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }

    private static IXLWorksheet PickDataSheet(XLWorkbook workbook) =>
        workbook.Worksheets.FirstOrDefault(sheet => string.Equals(sheet.Name, DataSheetName, StringComparison.OrdinalIgnoreCase))
        ?? workbook.Worksheet(1);

    private static ImportSheet ReadSheet(IXLWorksheet sheet, int maxRows)
    {
        var lastColumn = Math.Min(sheet.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0, MaxColumns);
        var headers = Enumerable.Range(1, lastColumn).Select(column => sheet.Cell(1, column).GetString().Trim()).ToList();
        var rows = new List<ImportSheetRow>();
        var dataRowCount = 0;

        foreach (var row in sheet.RowsUsed(XLCellsUsedOptions.Contents))
        {
            var rowNumber = row.RowNumber();
            if (rowNumber == 1)
            {
                continue;
            }

            // Past the cap a row is only counted, for the "too many rows" message; its cells are never read.
            if (rows.Count >= maxRows)
            {
                dataRowCount++;
                continue;
            }

            var cells = new ImportCell[lastColumn];
            for (var column = 0; column < lastColumn; column++)
            {
                cells[column] = ToCell(row.Cell(column + 1).Value);
            }

            // A row holding only spaces is blank, not a pupil with every field missing.
            if (cells.All(cell => cell.IsBlank))
            {
                continue;
            }

            dataRowCount++;
            rows.Add(new ImportSheetRow(rowNumber, cells));
        }

        return new ImportSheet(headers, rows, dataRowCount);
    }

    private static ImportCell ToCell(XLCellValue value) => value.Type switch
    {
        XLDataType.Text => new ImportCell(value.GetText().Trim(), null, null),
        XLDataType.Number => new ImportCell(value.GetNumber().ToString(CultureInfo.InvariantCulture), value.GetNumber(), null),
        XLDataType.DateTime => new ImportCell(value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null, value.GetDateTime()),
        XLDataType.Boolean => new ImportCell(value.GetBoolean() ? "TRUE" : "FALSE", null, null),
        XLDataType.TimeSpan => new ImportCell(value.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture), null, null),
        XLDataType.Error => new ImportCell("#ERROR", null, null),
        _ => ImportCell.Blank,
    };
}
