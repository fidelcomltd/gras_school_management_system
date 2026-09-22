using System.Globalization;
using System.Text.Json;
using SchoolManagement.Application.Abstractions.Results;

namespace SchoolManagement.Application.Results.Sheets;

/// <summary>Which of the school's two sheets a pupil gets (Appendix E.1, F).</summary>
public enum SheetSection
{
    /// <summary>Appendix E: development domains, no attendance.</summary>
    Nursery,

    /// <summary>Appendix F: affective and psychomotor traits, attendance.</summary>
    Primary,
}

/// <summary>One assessment column: generated from the snapshot's component list, never a fixed count (6.2.13).</summary>
/// <param name="Label">e.g. 1st CA.</param>
/// <param name="MaxMark">e.g. 20.</param>
/// <param name="IsExamination">The exam column prints ABS for an absent pupil.</param>
public sealed record SheetColumn(string Label, int MaxMark, bool IsExamination);

/// <summary>One subject row. A null cell prints blank; never a zero or a dash (C.8 rule 3).</summary>
/// <param name="Subject">Name from the snapshot.</param>
/// <param name="Cells">One per column: a mark, <c>ABS</c>, or null.</param>
/// <param name="Total">Computed total, or null.</param>
/// <param name="Grade">Computed band letter.</param>
/// <param name="Comment">The band's word.</param>
public sealed record SheetSubjectRow(string Subject, IReadOnlyList<string?> Cells, int? Total, string? Grade, string? Comment);

/// <summary>One rated row: a trait or an indicator.</summary>
/// <param name="Name">From the snapshot.</param>
/// <param name="PointCode">The chosen scale point's code (E, S, I, N), or null when unrated.</param>
/// <param name="Comment">Nursery only, optional.</param>
public sealed record SheetRatingItem(string Name, string? PointCode, string? Comment);

/// <summary>A rating block: a nursery domain, or a primary trait group.</summary>
/// <param name="Name">Block heading.</param>
/// <param name="PointCodes">The scale's column headings, in order.</param>
/// <param name="HasComments">Nursery domains that allow indicator comments.</param>
/// <param name="Items">Rows in display order.</param>
public sealed record SheetRatingBlock(string Name, IReadOnlyList<string> PointCodes, bool HasComments, IReadOnlyList<SheetRatingItem> Items);

/// <summary>A grade key row.</summary>
/// <param name="Grade">Band letter.</param>
/// <param name="Range">e.g. 85-89.</param>
/// <param name="Word">e.g. Excellent.</param>
public sealed record SheetGradeKeyRow(string Grade, string Range, string Word);

/// <summary>Primary attendance (F.1). Absent is derived, never typed.</summary>
/// <param name="Opened">Times school opened.</param>
/// <param name="Present">Times present.</param>
/// <param name="Absent">Opened minus present.</param>
public sealed record SheetAttendance(int? Opened, int? Present, int? Absent);

/// <summary>
/// The render-ready sheet (Appendices E and F; C.8). Every value is read from the stored rows and the publication
/// snapshot. Nothing is computed here beyond formatting, age, absent-days and the grand totals the contract names.
/// </summary>
public sealed record ResultSheet(
    SheetSection Section,
    string SchoolName,
    Guid? LogoUploadGroupId,
    Guid? SignatureUploadGroupId,
    string PupilName,
    string RegistrationNumber,
    int? Age,
    string AcademicYear,
    string TermName,
    string ClassName,
    string? Instructor,
    DateOnly? NextTermBegins,
    string? OverallGrade,
    decimal? TermAverage,
    string? RevisionNotice,
    IReadOnlyList<SheetColumn> Columns,
    IReadOnlyList<SheetSubjectRow> Subjects,
    IReadOnlyList<int?> GrandTotals,
    IReadOnlyList<SheetRatingBlock> RatingBlocks,
    string? RatingKey,
    SheetAttendance? Attendance,
    string? TeacherComment,
    string? HeadTeacherComment,
    IReadOnlyList<SheetGradeKeyRow> GradeKey,
    DateTimeOffset? IssuedAt);

/// <summary>Builds a <see cref="ResultSheet"/> from <see cref="ResultSheetData"/>. Pure, so it is unit-tested directly.</summary>
public static class ResultSheetBuilder
{
    /// <summary>Builds the sheet, or null when the set has no snapshot (never published).</summary>
    public static ResultSheet? Build(ResultSheetData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.SnapshotJson is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(data.SnapshotJson);
        var root = document.RootElement;
        var settings = root.GetProperty("settings");

        var ratesTraits = TryGet(root, "section", "ratesTraits")?.GetBoolean() ?? true;
        var section = ratesTraits ? SheetSection.Primary : SheetSection.Nursery;
        var sectionId = TryGet(root, "section", "id")?.GetGuid();
        var term = root.GetProperty("term");
        var termEnd = Date(TryGet(term, "endDate"));

        var components = Array(settings, "assessment", "components")
            .OrderBy(component => component.GetProperty("displayOrder").GetInt32())
            .Select(component => new
            {
                Id = component.GetProperty("id").GetString()!,
                Label = Str(component, "shortLabel") ?? Str(component, "name") ?? string.Empty,
                Max = component.GetProperty("maxMark").GetInt32(),
                IsExam = component.GetProperty("isExamination").GetBoolean(),
            })
            .ToList();
        var columns = components.Select(component => new SheetColumn(component.Label, component.Max, component.IsExam)).ToList();

        var scores = data.Scores.Where(score => !score.Voided).ToDictionary(score => score.SubjectId);
        var lines = data.Lines.ToDictionary(line => line.SubjectId);
        var subjects = Array(root, "subjects")
            .OrderBy(subject => subject.GetProperty("displayOrder").GetInt32())
            .Select(subject =>
            {
                var subjectId = subject.GetProperty("subjectId").GetGuid();
                scores.TryGetValue(subjectId, out var score);
                lines.TryGetValue(subjectId, out var line);
                var marks = score is null ? null : JsonSerializer.Deserialize<Dictionary<string, int>>(score.ComponentMarksJson);
                var cells = components.Select(component => component.IsExam
                        ? score is null ? null : score.ExamAbsent ? "ABS" : score.ExamMark?.ToString(CultureInfo.InvariantCulture)
                        : marks is not null && marks.TryGetValue(component.Id, out var mark) ? mark.ToString(CultureInfo.InvariantCulture) : null)
                    .ToList();
                return new SheetSubjectRow(Str(subject, "subjectName") ?? string.Empty, cells, line?.SubjectTotal, line?.Grade, line?.Remark);
            })
            .ToList();

        var grandTotals = columns.Select((_, index) => Sum(subjects.Select(row => row.Cells[index]))).ToList();
        grandTotals.Add(subjects.Any(row => row.Total is not null) ? subjects.Sum(row => row.Total ?? 0) : null);

        var scales = Array(settings, "ratingScales", "scales").ToDictionary(
            scale => scale.GetProperty("id").GetGuid(),
            scale => Array(scale, "points").OrderBy(point => point.GetProperty("pointOrder").GetInt32())
                .Select(point => (Id: point.GetProperty("id").GetGuid(), Code: Str(point, "pointCode") ?? string.Empty, Label: Str(point, "pointLabel") ?? string.Empty))
                .ToList());

        var blocks = new List<SheetRatingBlock>();
        string? ratingKey = null;
        if (section == SheetSection.Primary)
        {
            var traits = settings.GetProperty("traits");
            var ratings = data.TraitRatings.ToDictionary(rating => rating.ItemId);
            foreach (var (domain, heading, scaleProperty) in new[] { ("Affective", "Affective Domain", "affectiveRatingScaleId"), ("Psychomotor", "Psychomotor", "psychomotorRatingScaleId") })
            {
                var points = TryGet(traits, scaleProperty) is { ValueKind: JsonValueKind.String } scaleId && scales.TryGetValue(scaleId.GetGuid(), out var found) ? found : [];
                var items = Array(traits, "traits")
                    .Where(trait => Str(trait, "domain") == domain && Str(trait, "status") == "Active")
                    .OrderBy(trait => trait.GetProperty("displayOrder").GetInt32())
                    .Select(trait => new SheetRatingItem(
                        Str(trait, "name") ?? string.Empty,
                        ratings.TryGetValue(trait.GetProperty("id").GetGuid(), out var rating) ? points.FirstOrDefault(point => point.Id == rating.PointId).Code : null,
                        null))
                    .ToList();
                blocks.Add(new SheetRatingBlock(heading, points.Select(point => point.Code).ToList(), false, items));
                ratingKey ??= Key(points);
            }
        }
        else
        {
            var ratings = data.DevelopmentRatings.ToDictionary(rating => rating.ItemId);
            foreach (var domain in Array(settings, "developmentDomains", "domains")
                .Where(domain => Str(domain, "status") == "Active" && (sectionId is null || TryGet(domain, "sectionId")?.GetGuid() == sectionId))
                .OrderBy(domain => domain.GetProperty("displayOrder").GetInt32()))
            {
                var points = scales.TryGetValue(domain.GetProperty("ratingScaleId").GetGuid(), out var found) ? found : [];
                var hasComments = TryGet(domain, "allowsIndicatorComment")?.GetBoolean() ?? false;
                var items = Array(domain, "indicators")
                    .Where(indicator => Str(indicator, "status") == "Active")
                    .OrderBy(indicator => indicator.GetProperty("displayOrder").GetInt32())
                    .Select(indicator =>
                    {
                        var rated = ratings.TryGetValue(indicator.GetProperty("id").GetGuid(), out var rating);
                        return new SheetRatingItem(
                            Str(indicator, "name") ?? string.Empty,
                            rated ? points.FirstOrDefault(point => point.Id == rating!.PointId).Code : null,
                            rated && hasComments ? rating!.Comment : null);
                    })
                    .ToList();
                blocks.Add(new SheetRatingBlock((Str(domain, "name") ?? string.Empty).ToUpperInvariant(), points.Select(point => point.Code).ToList(), hasComments, items));
                ratingKey ??= Key(points);
            }
        }

        var opened = TryGet(term, "timesSchoolOpened") is { ValueKind: JsonValueKind.Number } openedValue ? openedValue.GetInt32() : (int?)null;
        var attendance = section == SheetSection.Primary
            ? new SheetAttendance(opened, data.TimesPresent, opened is { } o && data.TimesPresent is { } p ? o - p : null)
            : null;

        var gradeKey = Array(settings, "grading", "bands")
            .OrderBy(band => band.GetProperty("displayOrder").GetInt32())
            .Select(band => new SheetGradeKeyRow(
                Str(band, "gradeLetter") ?? string.Empty,
                $"{band.GetProperty("lowerBound").GetDecimal():0.##}-{band.GetProperty("upperBound").GetDecimal():0.##}",
                Str(band, "remark") ?? string.Empty))
            .ToList();

        string? revision = null;
        if (data.RevisionNumber > 1 && data.PublishedAt is { } issued)
        {
            revision = data.PreviousPublishedAt is { } previous
                ? $"Revised result, issued {Wat(issued):dd/MM/yyyy}. This replaces the version issued {Wat(previous):dd/MM/yyyy}."
                : $"Revised result, issued {Wat(issued):dd/MM/yyyy}.";
        }

        var termName = Str(term, "name") ?? string.Empty;
        var armName = TryGet(root, "arm", "displayName")?.GetString() ?? string.Empty;
        return new ResultSheet(
            section,
            TryGet(settings, "identity", "schoolName")?.GetString() ?? string.Empty,
            TryGet(root, "logoUploadGroupId") is { ValueKind: JsonValueKind.String } logo ? logo.GetGuid() : null,
            TryGet(root, "signatureUploadGroupId") is { ValueKind: JsonValueKind.String } signature ? signature.GetGuid() : null,
            $"{data.PupilSurname.ToUpperInvariant()} {data.PupilGivenNames}".Trim(),
            data.RegistrationNumber,
            termEnd is { } end ? AgeAt(data.DateOfBirth, end) : null,
            TryGet(root, "session", "name")?.GetString() ?? string.Empty,
            termName,
            section == SheetSection.Primary ? $"{armName}, {termName}" : armName,
            TryGet(root, "formTeacherName")?.GetString(),
            Date(TryGet(term, "nextResumptionDate")),
            data.OverallGrade,
            data.Average,
            revision,
            columns,
            subjects,
            grandTotals,
            blocks,
            ratingKey,
            attendance,
            data.TeacherComment,
            data.HeadTeacherComment,
            gradeKey,
            data.PublishedAt);
    }

    /// <summary>Whole years at <paramref name="asOf"/> (6.5.3).</summary>
    public static int AgeAt(DateOnly dateOfBirth, DateOnly asOf)
    {
        var age = asOf.Year - dateOfBirth.Year;
        return dateOfBirth > asOf.AddYears(-age) ? age - 1 : age;
    }

    private static int? Sum(IEnumerable<string?> cells)
    {
        var numbers = cells.Where(cell => int.TryParse(cell, NumberStyles.None, CultureInfo.InvariantCulture, out _)).Select(cell => int.Parse(cell!, CultureInfo.InvariantCulture)).ToList();
        return numbers.Count == 0 ? null : numbers.Sum();
    }

    private static string? Key(List<(Guid Id, string Code, string Label)> points) =>
        points.Count == 0 ? null : string.Join("  ", points.Select(point => $"{point.Code}-{point.Label}"));

    private static DateTimeOffset Wat(DateTimeOffset value) => value.ToOffset(TimeSpan.FromHours(1));

    private static DateOnly? Date(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.String } value ? DateOnly.Parse(value.GetString()!, CultureInfo.InvariantCulture) : null;

    private static string? Str(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static JsonElement? TryGet(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current) || current.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
        }

        return current;
    }

    private static List<JsonElement> Array(JsonElement element, params string[] path) =>
        TryGet(element, path) is { ValueKind: JsonValueKind.Array } array ? array.EnumerateArray().ToList() : [];
}
