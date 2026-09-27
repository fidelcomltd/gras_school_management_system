using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Reports.Results;

/// <summary>Arm broadsheet filters.</summary>
/// <param name="TermId">The term (its session follows).</param>
/// <param name="ArmId">The arm.</param>
public sealed record BroadsheetFilters(string? TermId, string? ArmId) : IReportFilters;

/// <summary>
/// Spec 15 section 10, arm broadsheet: one row per pupil, a CA / Exam / Total group per subject, then total, average, arm and
/// level position, sorted by arm position. Reads the computed rows in any state, because the head teacher approves from it.
/// </summary>
internal sealed class BroadsheetReport(IReportReader reader) : ReportBuilder<BroadsheetFilters>
{
    public override string Key => "broadsheet";

    protected override async Task<Result<ReportDto>> BuildAsync(BroadsheetFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryId(filters.TermId, "termId", out var termId, out var error) || !TryId(filters.ArmId, "armId", out var armId, out error))
        {
            return Result.Failure<ReportDto>(error);
        }

        if (!context.Scope.Allows(armId))
        {
            return Result.Failure<ReportDto>(OutOfScope());
        }

        var term = await reader.FindTermAsync(termId, cancellationToken).ConfigureAwait(false);
        var arm = term is null ? null : (await reader.ListArmsAsync(term.SessionId, cancellationToken).ConfigureAwait(false)).FirstOrDefault(candidate => candidate.ArmId == armId);
        if (term is null || arm is null)
        {
            return Result.Failure<ReportDto>(Error.NotFound("report.not_found", "No class was found for that term."));
        }

        var set = (await reader.ListResultSetsAsync(termId, cancellationToken).ConfigureAwait(false)).FirstOrDefault(candidate => candidate.ArmId == armId);
        IReadOnlyCollection<Guid> setIds = set is null ? [] : [set.ResultSetId];
        var results = await reader.ListTermResultsAsync(setIds, cancellationToken).ConfigureAwait(false);
        var lines = (await reader.ListSubjectLinesAsync(setIds, cancellationToken).ConfigureAwait(false))
            .ToDictionary(line => (line.PupilId, line.SubjectId));
        var pupils = await reader.FindPupilsAsync([.. results.Select(result => result.PupilId)], cancellationToken).ConfigureAwait(false);
        var (mapped, names) = await reader.ListSubjectsAsync(termId, [arm.LevelId], cancellationToken).ConfigureAwait(false);

        // Mapped subjects in sheet order, then any subject with lines that is no longer mapped, by name.
        var subjects = mapped.Select(subject => (subject.SubjectId, subject.Name))
            .Concat(lines.Keys.Select(key => key.SubjectId).Distinct()
                .Where(id => mapped.All(subject => subject.SubjectId != id))
                .Select(id => (SubjectId: id, Name: names.GetValueOrDefault(id, "Unknown subject")))
                .OrderBy(subject => subject.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var columns = new List<ReportColumnDto>
        {
            new("Pos.", ReportAlign.Right),
            new("Name", ReportAlign.Left),
            new("Reg. no.", ReportAlign.Left),
        };
        foreach (var (_, name) in subjects)
        {
            columns.Add(new("CA", ReportAlign.Right, name));
            columns.Add(new("Exam", ReportAlign.Right, name));
            columns.Add(new("Total", ReportAlign.Right, name));
        }

        columns.Add(new("Total", ReportAlign.Right));
        columns.Add(new("Average", ReportAlign.Right));
        columns.Add(new("Grade", ReportAlign.Center));
        columns.Add(new("Level pos.", ReportAlign.Right));

        var rows = results
            .Select(result => (Term: result, Pupil: pupils.GetValueOrDefault(result.PupilId)))
            .OrderBy(entry => entry.Term.ArmPosition ?? int.MaxValue)
            .ThenBy(entry => entry.Pupil?.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(entry =>
            {
                var cells = new List<string?>
                {
                    ReportText.Position(entry.Term.ArmPosition, entry.Term.ArmTied),
                    entry.Pupil?.DisplayName ?? "Unknown pupil",
                    entry.Pupil?.RegistrationNumber,
                };
                foreach (var (subjectId, _) in subjects)
                {
                    var line = lines.GetValueOrDefault((entry.Term.PupilId, subjectId));
                    cells.Add(line is null ? null : ReportText.Number(line.CaTotal));
                    cells.Add(line?.ExamMark is { } exam ? ReportText.Number(exam) : line is null ? null : "ABS");
                    cells.Add(line is null ? null : ReportText.Number(line.SubjectTotal));
                }

                cells.Add(ReportText.Number(entry.Term.TotalObtained));
                cells.Add(ReportText.Decimal(entry.Term.Average));
                cells.Add(entry.Term.Grade);
                cells.Add(ReportText.Position(entry.Term.LevelPosition, entry.Term.LevelTied));
                return new ReportRowDto(ReportRowKind.Data, cells);
            })
            .ToList();

        var notes = new List<string>();
        if (set is null || !set.Computed)
        {
            notes.Add("This class's results have not been computed for the term yet.");
        }
        else if (set.State != ResultSetState.Published)
        {
            notes.Add($"Not yet published ({ReportText.State(set.State)}): figures change if marks are corrected and computed again.");
        }

        return Result.Success(Report(
            context,
            "Arm broadsheet",
            [$"Class: {arm.Name}", $"{term.Name}, {term.SessionName}"],
            columns,
            rows,
            notes,
            ReportOrientation.Landscape));
    }
}
