using System.Text.Json;
using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Promotion;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Reports.Results;

/// <summary>Annual cumulative report filters: an arm or a level of the session.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="ArmId">One arm; or give <paramref name="LevelId"/>.</param>
/// <param name="LevelId">Every arm of a level.</param>
public sealed record AnnualCumulativeFilters(string? SessionId, string? ArmId, string? LevelId) : IReportFilters;

/// <summary>Promotion list filters.</summary>
/// <param name="SessionId">The session being promoted from.</param>
/// <param name="LevelId">One level only.</param>
/// <param name="Outcome">One outcome only (<c>Promoted</c>, <c>Repeat</c>, <c>PromotedOnTrial</c>, <c>Graduated</c>).</param>
public sealed record PromotionListFilters(string? SessionId, string? LevelId, string? Outcome) : IReportFilters;

/// <summary>What the annual reports share: the outcome in words, and a pupil's promotion as decided or as proposed.</summary>
internal static class AnnualText
{
    public static string Outcome(PromotionDecisionOutcome outcome) => outcome switch
    {
        PromotionDecisionOutcome.PromotedOnTrial => "Promoted on trial",
        _ => outcome.ToString(),
    };

    public static string Outcome(PromotionOutcome outcome) => outcome switch
    {
        PromotionOutcome.PromotedOnTrial => "Promoted on trial",
        _ => outcome.ToString(),
    };

    /// <summary>The committed decision when there is one, otherwise the computed proposal, marked as such.</summary>
    public static string Status(ReportAnnualResult result, ReportPromotionDecision? decision) =>
        decision is null ? $"{Outcome(result.ProposedOutcome)} (proposed)" : Outcome(decision.Outcome);

    public static string? Average(decimal? value) => value is { } average ? ReportText.Decimal(average) : null;
}

/// <summary>
/// Spec 15 section 10, annual cumulative report: per pupil the three term averages, the cumulative average and grade, the
/// annual position and the promotion status (the committed decision, or the proposal until promotion runs). Available once
/// annual computation has run.
/// </summary>
internal sealed class AnnualCumulativeReport(IReportReader reader) : ReportBuilder<AnnualCumulativeFilters>
{
    public override string Key => "annual-cumulative";

    protected override async Task<Result<ReportDto>> BuildAsync(AnnualCumulativeFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryId(filters.SessionId, "sessionId", out var sessionId, out var error)
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

        var session = await reader.FindSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Result.Failure<ReportDto>(NotFound("No session was found with that id."));
        }

        var arms = (await reader.ListArmsAsync(sessionId, cancellationToken).ConfigureAwait(false))
            .Where(arm => (armId is null || arm.ArmId == armId) && (levelId is null || arm.LevelId == levelId) && context.Scope.Allows(arm.ArmId))
            .ToDictionary(arm => arm.ArmId);
        if (arms.Count == 0)
        {
            return Result.Failure<ReportDto>(NotFound("No class you can see matches those filters in that session."));
        }

        var results = (await reader.ListAnnualResultsAsync(sessionId, cancellationToken).ConfigureAwait(false))
            .Where(result => arms.ContainsKey(result.ArmId))
            .ToList();
        var decisions = (await reader.ListPromotionDecisionsAsync(sessionId, cancellationToken).ConfigureAwait(false)).ToDictionary(decision => decision.PupilId);
        var pupils = await reader.FindPupilsAsync([.. results.Select(result => result.PupilId)], cancellationToken).ConfigureAwait(false);
        var byLevel = levelId is not null;

        var columns = new List<ReportColumnDto> { new("Pos.", ReportAlign.Right), new("Name", ReportAlign.Left), new("Reg. no.", ReportAlign.Left) };
        if (byLevel)
        {
            columns.Add(new("Class", ReportAlign.Left));
        }

        columns.AddRange(
        [
            new("First", ReportAlign.Right, "Term averages"),
            new("Second", ReportAlign.Right, "Term averages"),
            new("Third", ReportAlign.Right, "Term averages"),
            new("Cumulative", ReportAlign.Right),
            new("Grade", ReportAlign.Center),
            new("Promotion", ReportAlign.Left),
        ]);

        var rows = results
            .OrderBy(result => byLevel ? arms[result.ArmId].Name : string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Position ?? int.MaxValue)
            .ThenBy(result => pupils.GetValueOrDefault(result.PupilId)?.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(result =>
            {
                var pupil = pupils.GetValueOrDefault(result.PupilId);
                var cells = new List<string?> { ReportText.Position(result.Position, result.Tied), pupil?.DisplayName ?? "Unknown pupil", pupil?.RegistrationNumber };
                if (byLevel)
                {
                    cells.Add(arms[result.ArmId].Name);
                }

                cells.AddRange(result.TermAverages.Select(AnnualText.Average));
                cells.Add(ReportText.Decimal(result.CumulativeAverage));
                cells.Add(result.Grade);
                cells.Add(AnnualText.Status(result, decisions.GetValueOrDefault(result.PupilId)));
                return new ReportRowDto(ReportRowKind.Data, cells);
            })
            .ToList();

        var notes = new List<string>();
        if (rows.Count == 0)
        {
            notes.Add("Annual results have not been computed for these classes yet: they are, once Third Term is published.");
        }
        else if (decisions.Count == 0)
        {
            notes.Add("Promotion has not run for this session: the promotion column shows each pupil's proposal.");
        }

        return Result.Success(Report(
            context,
            "Annual cumulative report",
            [byLevel ? $"Level: {arms.Values.First().LevelName}" : $"Class: {arms.Values.First().Name}", $"Session: {session.Name}"],
            columns,
            rows,
            notes,
            ReportOrientation.Landscape));
    }
}

/// <summary>
/// Spec 15 section 10, promotion list: per pupil the arm, annual average, core subject results, the proposed and the final
/// outcome, the target arm, and the reason where an override was recorded. The document the school files. Before promotion
/// runs, the final outcome and target are blank.
/// </summary>
internal sealed class PromotionListReport(IReportReader reader) : ReportBuilder<PromotionListFilters>
{
    public override string Key => "promotion-list";

    protected override async Task<Result<ReportDto>> BuildAsync(PromotionListFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryId(filters.SessionId, "sessionId", out var sessionId, out var error) || !TryOptionalId(filters.LevelId, "levelId", out var levelId, out error))
        {
            return Result.Failure<ReportDto>(error);
        }

        if (!string.IsNullOrEmpty(filters.Outcome) && !Enum.TryParse<PromotionDecisionOutcome>(filters.Outcome, ignoreCase: true, out _))
        {
            return Result.Failure<ReportDto>(Error.Validation("report.filter", "outcome must be Promoted, Repeat, PromotedOnTrial or Graduated."));
        }

        var session = await reader.FindSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Result.Failure<ReportDto>(NotFound("No session was found with that id."));
        }

        var arms = (await reader.ListArmsAsync(sessionId, cancellationToken).ConfigureAwait(false))
            .Where(arm => (levelId is null || arm.LevelId == levelId) && context.Scope.Allows(arm.ArmId))
            .ToDictionary(arm => arm.ArmId);
        if (levelId is not null && arms.Count == 0)
        {
            return Result.Failure<ReportDto>(NotFound("No class you can see is in that level in that session."));
        }

        var results = (await reader.ListAnnualResultsAsync(sessionId, cancellationToken).ConfigureAwait(false)).Where(result => arms.ContainsKey(result.ArmId)).ToList();
        var decisions = (await reader.ListPromotionDecisionsAsync(sessionId, cancellationToken).ConfigureAwait(false)).ToDictionary(decision => decision.PupilId);
        var targets = decisions.Count == 0
            ? new Dictionary<Guid, ReportArm>()
            : await reader.FindArmsAsync([.. decisions.Values.Where(decision => decision.TargetArmId is not null).Select(decision => decision.TargetArmId!.Value)], cancellationToken)
                .ConfigureAwait(false);
        var pupils = await reader.FindPupilsAsync([.. results.Select(result => result.PupilId)], cancellationToken).ConfigureAwait(false);
        var (coreIds, passMark) = await reader.GetCoreRulesAsync(cancellationToken).ConfigureAwait(false);
        var coreNames = coreIds.Count == 0 ? new Dictionary<Guid, string>() : await reader.FindSubjectNamesAsync(coreIds, cancellationToken).ConfigureAwait(false);

        var rows = results
            .Select(result => (Row: result, Decision: decisions.GetValueOrDefault(result.PupilId), Pupil: pupils.GetValueOrDefault(result.PupilId)))
            .Where(entry => string.IsNullOrEmpty(filters.Outcome)
                || string.Equals(entry.Decision?.Outcome.ToString() ?? entry.Row.ProposedOutcome.ToString(), filters.Outcome, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => arms[entry.Row.ArmId].LevelOrder)
            .ThenBy(entry => arms[entry.Row.ArmId].Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Pupil?.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new ReportRowDto(ReportRowKind.Data,
            [
                entry.Pupil?.DisplayName ?? "Unknown pupil",
                entry.Pupil?.RegistrationNumber,
                arms[entry.Row.ArmId].Name,
                ReportText.Decimal(entry.Row.CumulativeAverage),
                Core(entry.Row.SubjectsJson, coreIds, coreNames, passMark),
                AnnualText.Outcome(entry.Row.ProposedOutcome),
                entry.Decision is null ? null : AnnualText.Outcome(entry.Decision.Outcome),
                entry.Decision?.TargetArmId is { } target ? targets.GetValueOrDefault(target)?.Name : null,
                entry.Decision?.Reason,
            ]))
            .ToList();

        var filterLines = new List<string> { $"Session: {session.Name}" };
        if (levelId is not null)
        {
            filterLines.Add($"Level: {arms.Values.First().LevelName}");
        }

        if (!string.IsNullOrEmpty(filters.Outcome))
        {
            filterLines.Add($"Outcome: {filters.Outcome}");
        }

        var notes = new List<string> { $"Core subjects and the pass mark ({ReportText.Number(passMark)}) are the result rules as configured now." };
        if (decisions.Count == 0)
        {
            notes.Add("Promotion has not run for this session: the final outcome and target class are blank until it does.");
        }

        return Result.Success(Report(
            context,
            "Promotion list",
            filterLines,
            [
                new("Name", ReportAlign.Left),
                new("Reg. no.", ReportAlign.Left),
                new("Class", ReportAlign.Left),
                new("Annual average", ReportAlign.Right),
                new("Core subjects", ReportAlign.Left),
                new("Proposed", ReportAlign.Left),
                new("Final", ReportAlign.Left),
                new("Target class", ReportAlign.Left),
                new("Reason", ReportAlign.Left),
            ],
            rows,
            notes,
            ReportOrientation.Landscape));
    }

    // "Mathematics 68 pass; English 45 fail", from the annual row's per-subject means; a core subject not taken says so.
    private static string? Core(string subjectsJson, IReadOnlyList<Guid> coreIds, IReadOnlyDictionary<Guid, string> names, int passMark)
    {
        if (coreIds.Count == 0)
        {
            return null;
        }

        var means = new Dictionary<Guid, decimal>();
        using (var document = JsonDocument.Parse(subjectsJson))
        {
            foreach (var subject in document.RootElement.EnumerateArray())
            {
                if (subject.TryGetProperty("subjectId", out var id) && id.TryGetGuid(out var subjectId) && subject.TryGetProperty("mean", out var mean))
                {
                    means[subjectId] = mean.GetDecimal();
                }
            }
        }

        return string.Join("; ", coreIds.Select(id =>
        {
            var name = names.GetValueOrDefault(id, "Unknown subject");
            return means.TryGetValue(id, out var value)
                ? $"{name} {ReportText.Decimal(value)} {(value >= passMark ? "pass" : "fail")}"
                : $"{name} not taken";
        }));
    }
}
