using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Reports.Results;

/// <summary>Result entry progress filters.</summary>
/// <param name="TermId">The term.</param>
/// <param name="LevelId">One level only.</param>
/// <param name="State">One result-set state only (<c>NotStarted</c>, <c>Draft</c>, <c>AwaitingApproval</c>, ...).</param>
public sealed record ResultEntryProgressFilters(string? TermId, string? LevelId, string? State) : IReportFilters;

/// <summary>
/// Spec 15 section 10, result entry progress: one row per arm for the term, with subjects mapped, mark cells complete of total,
/// traits, attendance and remarks complete, and the result set's state. Computed by the same evaluator as each arm's
/// readiness grid, so the two cannot disagree. The end-of-week-eleven report that says which teachers to chase.
/// </summary>
internal sealed class ResultEntryProgressReport(
    IReportReader reader,
    IArmRepository arms,
    ITermRepository terms,
    IResultSetRepository resultSets,
    IResultSetReadinessEvaluator readiness)
    : ReportBuilder<ResultEntryProgressFilters>
{
    private const string NotStarted = "NotStarted";

    public override string Key => "result-entry-progress";

    protected override async Task<Result<ReportDto>> BuildAsync(ResultEntryProgressFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryId(filters.TermId, "termId", out var termId, out var error) || !TryOptionalId(filters.LevelId, "levelId", out var levelId, out error))
        {
            return Result.Failure<ReportDto>(error);
        }

        ResultSetState? wantedState = null;
        var notStartedOnly = string.Equals(filters.State, NotStarted, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(filters.State) && !notStartedOnly)
        {
            if (!Enum.TryParse<ResultSetState>(filters.State, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return Result.Failure<ReportDto>(Error.Validation("report.filter", "state must be NotStarted or a result-set state."));
            }

            wantedState = parsed;
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        var named = await reader.FindTermAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null || named is null)
        {
            return Result.Failure<ReportDto>(Error.NotFound("report.not_found", "No term was found with that id."));
        }

        var catalogue = (await reader.ListArmsAsync(term.SessionId, cancellationToken).ConfigureAwait(false))
            .Where(arm => (levelId is null || arm.LevelId == levelId) && context.Scope.Allows(arm.ArmId))
            .ToList();
        var wanted = catalogue.Select(arm => arm.ArmId).ToHashSet();
        var entities = (await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false))
            .Where(arm => wanted.Contains(arm.Id))
            .ToDictionary(arm => arm.Id);

        var rows = new List<ReportRowDto>();
        foreach (var arm in catalogue)
        {
            if (!entities.TryGetValue(arm.ArmId, out var entity))
            {
                continue;
            }

            var set = await resultSets.FindReadOnlyByArmTermAsync(arm.ArmId, termId, cancellationToken).ConfigureAwait(false);
            if ((notStartedOnly && set is not null) || (wantedState is { } state && set?.State != state))
            {
                continue;
            }

            var grid = await readiness.EvaluateAsync(entity, term, set, cancellationToken).ConfigureAwait(false);
            var counters = grid.Counters;
            rows.Add(new ReportRowDto(ReportRowKind.Data,
            [
                arm.Name,
                ReportText.Number(grid.Pupils.Count),
                ReportText.Number(grid.Subjects.Count),
                Of(counters.Marks),
                Of(counters.Ratings),
                Of(counters.Attendance),
                Of(counters.ClassTeacherRemarks),
                Of(counters.HeadTeacherRemarks),
                ReportText.State(set?.State),
            ]));
        }

        var filterLines = new List<string> { $"{named.Name}, {named.SessionName}" };
        if (levelId is not null && catalogue.Count > 0)
        {
            filterLines.Add($"Level: {catalogue[0].LevelName}");
        }

        if (!string.IsNullOrEmpty(filters.State))
        {
            filterLines.Add($"State: {(notStartedOnly ? "Not started" : ReportText.State(wantedState))}");
        }

        return Result.Success(Report(
            context,
            "Result entry progress",
            filterLines,
            [
                new("Class", ReportAlign.Left),
                new("Pupils", ReportAlign.Right),
                new("Subjects", ReportAlign.Right),
                new("Mark cells", ReportAlign.Right),
                new("Ratings", ReportAlign.Right),
                new("Attendance", ReportAlign.Right),
                new("Teacher's remarks", ReportAlign.Right),
                new("Head's remarks", ReportAlign.Right),
                new("State", ReportAlign.Left),
            ],
            rows,
            ["Each figure is complete of total, as on the class's own progress screen."],
            ReportOrientation.Landscape));
    }

    private static string Of(ReadinessCounterDto counter) => $"{ReportText.Number(counter.Complete)} of {ReportText.Number(counter.Total)}";
}
