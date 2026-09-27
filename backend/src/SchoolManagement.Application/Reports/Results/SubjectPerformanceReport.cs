using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Reports.Results;

/// <summary>Subject performance filters.</summary>
/// <param name="TermId">The term.</param>
/// <param name="LevelId">The level whose arms are compared.</param>
public sealed record SubjectPerformanceFilters(string? TermId, string? LevelId) : IReportFilters;

/// <summary>
/// Spec 15 section 10, subject performance: per subject, each arm of the level side by side (average, highest, lowest,
/// counted, absent from the exam, passing, pass rate), then the level as a whole. How a head teacher sees one arm's
/// Mathematics behind another's.
/// </summary>
internal sealed class SubjectPerformanceReport(IReportReader reader) : ReportBuilder<SubjectPerformanceFilters>
{
    public override string Key => "subject-performance";

    protected override async Task<Result<ReportDto>> BuildAsync(SubjectPerformanceFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryId(filters.TermId, "termId", out var termId, out var error) || !TryId(filters.LevelId, "levelId", out var levelId, out error))
        {
            return Result.Failure<ReportDto>(error);
        }

        var term = await reader.FindTermAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<ReportDto>(NotFound("No term was found with that id."));
        }

        var arms = (await reader.ListArmsAsync(term.SessionId, cancellationToken).ConfigureAwait(false))
            .Where(arm => arm.LevelId == levelId && context.Scope.Allows(arm.ArmId))
            .ToList();
        if (arms.Count == 0)
        {
            return Result.Failure<ReportDto>(NotFound("No class you can see is in that level in that term's session."));
        }

        var sets = (await reader.ListResultSetsAsync(termId, cancellationToken).ConfigureAwait(false))
            .Where(set => arms.Any(arm => arm.ArmId == set.ArmId))
            .ToDictionary(set => set.ResultSetId, set => set.ArmId);
        var lines = await reader.ListSubjectLinesAsync(sets.Keys, cancellationToken).ConfigureAwait(false);
        var mapped = await reader.ListMappedSubjectsAsync(termId, [levelId], cancellationToken).ConfigureAwait(false);
        var unmapped = lines.Select(line => line.SubjectId).Distinct().Where(id => mapped.All(subject => subject.SubjectId != id)).ToList();
        var names = unmapped.Count == 0 ? new Dictionary<Guid, string>() : await reader.FindSubjectNamesAsync(unmapped, cancellationToken).ConfigureAwait(false);
        var subjects = mapped.Select(subject => (Id: subject.SubjectId, subject.Name))
            .Concat(unmapped.Select(id => (Id: id, Name: names.GetValueOrDefault(id, "Unknown subject"))).OrderBy(subject => subject.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var rows = new List<ReportRowDto>();
        foreach (var (id, name) in subjects)
        {
            var subjectLines = lines.Where(line => line.SubjectId == id).ToList();
            if (subjectLines.Count == 0)
            {
                continue;
            }

            rows.Add(new(ReportRowKind.Heading, [name, null, null, null, null, null, null, null]));
            foreach (var arm in arms)
            {
                var armLines = subjectLines.Where(line => sets[line.ResultSetId] == arm.ArmId).ToList();
                if (armLines.Count > 0)
                {
                    rows.Add(Row(ReportRowKind.Data, arm.Name, armLines));
                }
            }

            rows.Add(Row(ReportRowKind.Subtotal, arms.Count == 1 ? "Class" : "All classes", subjectLines));
        }

        var notes = new List<string> { "Absent counts pupils marked absent from the examination; they are still counted." };
        if (context.Scope.Arms is not null)
        {
            notes.Add("Only the classes you have access to are shown.");
        }

        return Result.Success(Report(
            context,
            "Subject performance",
            [$"Level: {arms[0].LevelName}", $"{term.Name}, {term.SessionName}"],
            [
                new("Class", ReportAlign.Left),
                new("Average", ReportAlign.Right),
                new("Highest", ReportAlign.Right),
                new("Lowest", ReportAlign.Right),
                new("Counted", ReportAlign.Right),
                new("Absent", ReportAlign.Right),
                new("Passing", ReportAlign.Right),
                new("Pass rate", ReportAlign.Right),
            ],
            rows,
            notes,
            ReportOrientation.Landscape));
    }

    private static ReportRowDto Row(ReportRowKind kind, string label, List<ReportSubjectLine> lines)
    {
        var passing = lines.Count(line => line.IsPass);
        return new(kind,
        [
            label,
            ReportText.Decimal((decimal)lines.Average(line => line.SubjectTotal)),
            ReportText.Number(lines.Max(line => line.SubjectTotal)),
            ReportText.Number(lines.Min(line => line.SubjectTotal)),
            ReportText.Number(lines.Count),
            ReportText.Number(lines.Count(line => line.ExamMark is null)),
            ReportText.Number(passing),
            ReportText.Percent(passing, lines.Count),
        ]);
    }
}
