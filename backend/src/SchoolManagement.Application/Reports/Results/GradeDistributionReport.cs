using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Reports.Results;

/// <summary>Grade distribution filters: an arm or a level.</summary>
/// <param name="TermId">The term.</param>
/// <param name="ArmId">One arm; or give <paramref name="LevelId"/>.</param>
/// <param name="LevelId">Every arm of a level.</param>
public sealed record GradeDistributionFilters(string? TermId, string? ArmId, string? LevelId) : IReportFilters;

/// <summary>
/// Spec 15 section 10, grade distribution: counts and percentages of pupils in each grade band, per subject and overall, for
/// an arm or a level, with a bar drawn in text characters so it survives monochrome photocopying.
/// </summary>
internal sealed class GradeDistributionReport(IReportReader reader) : ReportBuilder<GradeDistributionFilters>
{
    private const int BarWidth = 20;

    public override string Key => "grade-distribution";

    protected override async Task<Result<ReportDto>> BuildAsync(GradeDistributionFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryId(filters.TermId, "termId", out var termId, out var error)
            || !TryOptionalId(filters.ArmId, "armId", out var armId, out error)
            || !TryOptionalId(filters.LevelId, "levelId", out var levelId, out error))
        {
            return Result.Failure<ReportDto>(error);
        }

        if ((armId is null) == (levelId is null))
        {
            return Result.Failure<ReportDto>(Error.Validation("report.filter", "Give either armId or levelId."));
        }

        if (armId is { } wanted && !context.Scope.Allows(wanted))
        {
            return Result.Failure<ReportDto>(OutOfScope());
        }

        var term = await reader.FindTermAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<ReportDto>(NotFound("No term was found with that id."));
        }

        var arms = (await reader.ListArmsAsync(term.SessionId, cancellationToken).ConfigureAwait(false))
            .Where(arm => (armId is null || arm.ArmId == armId) && (levelId is null || arm.LevelId == levelId) && context.Scope.Allows(arm.ArmId))
            .ToList();
        if (arms.Count == 0)
        {
            return Result.Failure<ReportDto>(NotFound("No class you can see matches those filters in that term's session."));
        }

        var armIds = arms.Select(arm => arm.ArmId).ToHashSet();
        var setIds = (await reader.ListResultSetsAsync(termId, cancellationToken).ConfigureAwait(false))
            .Where(set => armIds.Contains(set.ArmId))
            .Select(set => set.ResultSetId)
            .ToList();
        var lines = await reader.ListSubjectLinesAsync(setIds, cancellationToken).ConfigureAwait(false);
        var results = await reader.ListTermResultsAsync(setIds, cancellationToken).ConfigureAwait(false);
        var bands = await reader.ListGradeBandsAsync(cancellationToken).ConfigureAwait(false);
        var mapped = await reader.ListMappedSubjectsAsync(termId, [.. arms.Select(arm => arm.LevelId).Distinct()], cancellationToken).ConfigureAwait(false);
        var unmapped = lines.Select(line => line.SubjectId).Distinct().Where(id => mapped.All(subject => subject.SubjectId != id)).ToList();
        var names = unmapped.Count == 0 ? new Dictionary<Guid, string>() : await reader.FindSubjectNamesAsync(unmapped, cancellationToken).ConfigureAwait(false);

        var subjects = mapped.GroupBy(subject => subject.SubjectId).Select(group => (Id: group.Key, group.First().Name))
            .Concat(unmapped.Select(id => (Id: id, Name: names.GetValueOrDefault(id, "Unknown subject"))).OrderBy(subject => subject.Name, StringComparer.OrdinalIgnoreCase))
            .Where(subject => lines.Any(line => line.SubjectId == subject.Id))
            .ToList();

        var rows = new List<ReportRowDto>();
        foreach (var (id, name) in subjects)
        {
            Section(rows, name, lines.Where(line => line.SubjectId == id).Select(line => line.Grade).ToList(), bands);
        }

        if (results.Count > 0)
        {
            Section(rows, "Overall (term grade)", results.Select(result => result.Grade).ToList(), bands);
        }

        var scopeLine = levelId is not null ? $"Level: {arms[0].LevelName}" : $"Class: {arms[0].Name}";
        var notes = new List<string> { $"Each # is {100 / BarWidth}% of the pupils graded in that section." };
        if (levelId is not null && context.Scope.Arms is not null)
        {
            notes.Add("Only the classes you have access to are counted.");
        }

        return Result.Success(Report(
            context,
            "Grade distribution",
            [scopeLine, $"{term.Name}, {term.SessionName}"],
            [
                new("Grade", ReportAlign.Center),
                new("Range", ReportAlign.Center),
                new("Pupils", ReportAlign.Right),
                new("%", ReportAlign.Right),
                new("Bar", ReportAlign.Left),
            ],
            rows,
            notes));
    }

    // A heading, then one row per band (zero counts included, so every section has the same shape), then any grade letter
    // no longer among the bands, then the section's total.
    private static void Section(List<ReportRowDto> rows, string title, List<string> grades, IReadOnlyList<ReportGradeBand> bands)
    {
        rows.Add(new(ReportRowKind.Heading, [title, null, null, null, null]));
        var counts = grades.GroupBy(grade => grade, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var letters = bands.Select(band => (band.Letter, Range: $"{band.LowerBound}–{band.UpperBound}"))
            .Concat(counts.Keys.Where(letter => bands.All(band => band.Letter != letter)).Order(StringComparer.Ordinal).Select(letter => (Letter: letter, Range: "–")));
        foreach (var (letter, range) in letters)
        {
            var count = counts.GetValueOrDefault(letter);
            var width = grades.Count == 0 ? 0 : (int)Math.Round((decimal)BarWidth * count / grades.Count, MidpointRounding.AwayFromZero);
            rows.Add(new(ReportRowKind.Data, [letter, range, ReportText.Number(count), ReportText.Percent(count, grades.Count), new string('#', width)]));
        }

        rows.Add(new(ReportRowKind.Subtotal, ["Total", null, ReportText.Number(grades.Count), grades.Count == 0 ? "–" : "100%", null]));
    }
}
