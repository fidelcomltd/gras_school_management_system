using System.Globalization;
using System.Text.Json;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results.Sheets;

/// <summary>One subject across the year.</summary>
/// <param name="Subject">Name.</param>
/// <param name="TermTotals">First, second, third; null where not taken.</param>
/// <param name="Mean">Two places.</param>
/// <param name="Grade">Band letter.</param>
/// <param name="TermsTaken">Below three is footnoted.</param>
public sealed record AnnualSheetSubjectRow(string Subject, IReadOnlyList<int?> TermTotals, decimal Mean, string? Grade, int TermsTaken);

/// <summary>
/// The annual cumulative document (spec 6.7.10, Appendix C "ANNUAL REPORT SHEET"), render-ready. Read from the stored
/// <c>annual_result</c> row and the Third Term publication snapshot; nothing is computed here. The school's sheets print
/// no positions (Appendix F.7), and promotion status prints only after a promotion batch is committed, so neither is here.
/// </summary>
public sealed record AnnualSheet(
    string SchoolName,
    string? SchoolAddress,
    string? SchoolMotto,
    string? HeadTeacherName,
    Guid? LogoUploadGroupId,
    Guid? SignatureUploadGroupId,
    string PupilName,
    string RegistrationNumber,
    string ClassName,
    string AcademicYear,
    int TermsCounted,
    IReadOnlyList<decimal?> TermAverages,
    IReadOnlyList<int?> TermTotals,
    int GrandTotal,
    decimal CumulativeAverage,
    string CumulativeGrade,
    string CumulativeRemark,
    string? TermsCountedNote,
    IReadOnlyList<AnnualSheetSubjectRow> Subjects,
    IReadOnlyList<SheetGradeKeyRow> GradeKey,
    DateTimeOffset ComputedAt);

/// <summary>Builds an <see cref="AnnualSheet"/>. Pure.</summary>
public static class AnnualSheetBuilder
{
    /// <summary>The term column headings.</summary>
    public static readonly IReadOnlyList<string> TermLabels = ["1st Term", "2nd Term", "3rd Term"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Null when the Third Term snapshot is missing (not published).</summary>
    public static AnnualSheet? Build(AnnualSheetData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.ThirdTermSnapshotJson is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(data.ThirdTermSnapshotJson);
        var root = document.RootElement;
        var identity = Get(root, "settings", "identity");
        var row = data.Row;

        // Third Term's display order; a subject dropped before Third Term sorts after the rest.
        var order = Get(root, "subjects") is { ValueKind: JsonValueKind.Array } listed
            ? listed.EnumerateArray().ToDictionary(subject => subject.GetProperty("subjectId").GetGuid(), subject => subject.GetProperty("displayOrder").GetInt32())
            : [];
        var subjects = (JsonSerializer.Deserialize<List<AnnualSubjectResult>>(row.SubjectsJson, Json) ?? [])
            .OrderBy(subject => order.TryGetValue(subject.SubjectId, out var position) ? position : int.MaxValue)
            .Select(subject => new AnnualSheetSubjectRow(
                data.SubjectNames.TryGetValue(subject.SubjectId, out var name) ? name : string.Empty,
                subject.TermTotals,
                subject.Mean,
                subject.Grade,
                subject.TermsTaken))
            .ToList();

        var bands = Get(root, "settings", "grading", "bands") is { ValueKind: JsonValueKind.Array } array
            ? array.EnumerateArray()
                .OrderBy(band => band.GetProperty("displayOrder").GetInt32())
                .Select(band => new SheetGradeKeyRow(
                    band.GetProperty("gradeLetter").GetString() ?? string.Empty,
                    string.Create(CultureInfo.InvariantCulture, $"{band.GetProperty("lowerBound").GetDecimal():0.##}-{band.GetProperty("upperBound").GetDecimal():0.##}"),
                    band.TryGetProperty("remark", out var remark) ? remark.GetString() ?? string.Empty : string.Empty))
                .ToList()
            : [];

        return new AnnualSheet(
            Str(identity, "schoolName") ?? string.Empty,
            Str(identity, "address"),
            Str(identity, "motto"),
            Str(identity, "headTeacherName"),
            GuidOf(root, "logoUploadGroupId"),
            GuidOf(root, "signatureUploadGroupId"),
            $"{data.PupilSurname.ToUpperInvariant()} {data.PupilGivenNames}".Trim(),
            data.RegistrationNumber,
            Str(Get(root, "arm"), "displayName") ?? string.Empty,
            Str(Get(root, "session"), "name") ?? string.Empty,
            row.TermsCounted,
            [row.FirstTermAverage, row.SecondTermAverage, row.ThirdTermAverage],
            [row.FirstTermTotal, row.SecondTermTotal, row.ThirdTermTotal],
            row.GrandTotal,
            row.CumulativeAverage,
            row.CumulativeGrade,
            row.CumulativeRemark,
            row.TermsCounted switch
            {
                1 => "Not ranked annually: first term at this school.",
                2 => "Cumulative average based on 2 of 3 terms.",
                _ => null,
            },
            subjects,
            bands,
            row.ComputedAtUtc);
    }

    private static JsonElement? Get(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out element) || element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
        }

        return element;
    }

    private static string? Str(JsonElement? element, string property) =>
        element is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.String ? found.GetString() : null;

    private static Guid? GuidOf(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetGuid() : null;
}
