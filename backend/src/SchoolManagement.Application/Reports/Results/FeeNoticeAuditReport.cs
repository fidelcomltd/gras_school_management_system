using System.Globalization;
using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Reports.Results;

/// <summary>Fee notice audit filters.</summary>
/// <param name="TermId">The term whose sheets print the notice.</param>
/// <param name="LevelId">One level only.</param>
public sealed record FeeNoticeAuditFilters(string? TermId, string? LevelId) : IReportFilters;

/// <summary>
/// Spec 15 section 10.2, fee notice audit: per level, the configured fee lines and amounts for the term, and how many pupils
/// carry a typed outstanding figure and its total. Confirms what the sheets printed before they went home.
/// </summary>
internal sealed class FeeNoticeAuditReport(IReportReader reader) : ReportBuilder<FeeNoticeAuditFilters>
{
    public override string Key => "fee-notice-audit";

    protected override async Task<Result<ReportDto>> BuildAsync(FeeNoticeAuditFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryId(filters.TermId, "termId", out var termId, out var error) || !TryOptionalId(filters.LevelId, "levelId", out var levelId, out error))
        {
            return Result.Failure<ReportDto>(error);
        }

        var term = await reader.FindTermAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<ReportDto>(NotFound("No term was found with that id."));
        }

        var arms = (await reader.ListArmsAsync(term.SessionId, cancellationToken).ConfigureAwait(false))
            .Where(arm => (levelId is null || arm.LevelId == levelId) && context.Scope.Allows(arm.ArmId))
            .ToList();
        var levels = arms.GroupBy(arm => arm.LevelId).Select(group => (Id: group.Key, group.First().LevelName, Arms: group.Select(arm => arm.ArmId).ToHashSet())).ToList();
        var lines = await reader.ListFeeLinesAsync(termId, [.. levels.Select(level => level.Id)], cancellationToken).ConfigureAwait(false);
        var sets = (await reader.ListResultSetsAsync(termId, cancellationToken).ConfigureAwait(false))
            .Where(set => arms.Any(arm => arm.ArmId == set.ArmId))
            .ToList();
        var outstanding = await reader.SumOutstandingAsync([.. sets.Select(set => set.ResultSetId)], cancellationToken).ConfigureAwait(false);

        var rows = new List<ReportRowDto>();
        foreach (var level in levels)
        {
            rows.Add(new(ReportRowKind.Heading, [level.LevelName, null]));
            var levelLines = lines.Where(line => line.LevelId == level.Id).ToList();
            foreach (var line in levelLines)
            {
                rows.Add(new(ReportRowKind.Data, [line.Label, line.Amount is { } amount ? Naira(amount) : "Not set"]));
            }

            rows.Add(new(ReportRowKind.Subtotal, ["Total fees", Naira(levelLines.Sum(line => line.Amount ?? 0))]));
            var owing = sets.Where(set => level.Arms.Contains(set.ArmId))
                .Select(set => outstanding.GetValueOrDefault(set.ResultSetId))
                .Aggregate((Pupils: 0, Total: 0L), (sum, next) => (sum.Pupils + next.Pupils, sum.Total + next.Total));
            rows.Add(new(ReportRowKind.Data, ["Pupils with an outstanding figure", ReportText.Number(owing.Pupils)]));
            rows.Add(new(ReportRowKind.Data, ["Outstanding total", Naira(owing.Total)]));
        }

        var filterLines = new List<string> { $"{term.Name}, {term.SessionName}" };
        if (levelId is not null && levels.Count > 0)
        {
            filterLines.Add($"Level: {levels[0].LevelName}");
        }

        var notes = new List<string>
        {
            "Amounts are the fee notice as configured now. A published sheet printed the amounts frozen when it was published.",
        };
        if (context.Scope.Arms is not null)
        {
            notes.Add("Outstanding figures count only the classes you have access to.");
        }

        return Result.Success(Report(context, "Fee notice audit", filterLines, [new("Item", ReportAlign.Left), new("Naira", ReportAlign.Right)], rows, notes));
    }

    private static string Naira(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture);
}
