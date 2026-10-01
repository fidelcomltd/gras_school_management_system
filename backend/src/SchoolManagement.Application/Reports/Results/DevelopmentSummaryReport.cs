using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Reports.Results;

/// <summary>Development domain summary filters.</summary>
/// <param name="TermId">The term.</param>
/// <param name="ArmId">The (nursery) arm.</param>
public sealed record DevelopmentSummaryFilters(string? TermId, string? ArmId) : IReportFilters;

/// <summary>
/// Spec 15 section 10.2, development domain summary (nursery only): for one arm and term, how many pupils sit at each rating
/// point of each indicator, and how many of the class are not rated on it yet. Only the active domains and indicators the
/// sheet shows. When every domain uses one rating scale its point labels head the columns; otherwise the columns are the
/// point positions and each domain's heading spells out its own scale.
/// </summary>
internal sealed class DevelopmentSummaryReport(IReportReader reader) : ReportBuilder<DevelopmentSummaryFilters>
{
    public override string Key => "development-summary";

    protected override async Task<Result<ReportDto>> BuildAsync(DevelopmentSummaryFilters filters, ReportContext context, CancellationToken cancellationToken)
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
            return Result.Failure<ReportDto>(NotFound("No class was found for that term."));
        }

        var filterLines = new List<string> { $"Class: {arm.Name}", $"{term.Name}, {term.SessionName}" };
        var indicators = await reader.ListIndicatorsAsync(arm.SectionId, cancellationToken).ConfigureAwait(false);
        if (indicators.Count == 0)
        {
            return Result.Success(Report(context, "Development domain summary", filterLines, [new("Indicator", ReportAlign.Left)], [],
                ["This class's section rates traits, not development domains: this report is for nursery classes."]));
        }

        var scales = (await reader.ListRatingPointsAsync([.. indicators.Select(indicator => indicator.RatingScaleId).Distinct()], cancellationToken).ConfigureAwait(false))
            .GroupBy(point => point.RatingScaleId)
            .ToDictionary(group => group.Key, group => group.OrderBy(point => point.Order).ToList());
        var position = scales.Values.SelectMany(points => points.Select((point, index) => (point.PointId, Index: index))).ToDictionary(entry => entry.PointId, entry => entry.Index);
        var width = scales.Values.Max(points => points.Count);
        var oneScale = scales.Count == 1;

        var roster = (await reader.ListRosterAsync(armId, termId, cancellationToken).ConfigureAwait(false)).ToHashSet();
        var set = (await reader.ListResultSetsAsync(termId, cancellationToken).ConfigureAwait(false)).FirstOrDefault(candidate => candidate.ArmId == armId);
        var ratings = set is null ? [] : await reader.ListDevelopmentRatingsAsync(set.ResultSetId, cancellationToken).ConfigureAwait(false);

        var columns = new List<ReportColumnDto> { new("Indicator", ReportAlign.Left) };
        columns.AddRange(Enumerable.Range(0, width).Select(index => new ReportColumnDto(
            oneScale && index < scales.Values.First().Count ? scales.Values.First()[index].Label : $"Point {index + 1}", ReportAlign.Right)));
        columns.Add(new("Not rated", ReportAlign.Right));

        var rows = new List<ReportRowDto>();
        foreach (var domain in indicators.GroupBy(indicator => indicator.DomainId))
        {
            var first = domain.First();
            var heading = oneScale || !scales.TryGetValue(first.RatingScaleId, out var own)
                ? first.DomainName
                : $"{first.DomainName} ({string.Join(", ", own.Select((point, index) => $"{index + 1} = {point.Label}"))})";
            rows.Add(new(ReportRowKind.Heading, [heading, .. Enumerable.Repeat<string?>(null, width + 1)]));
            foreach (var indicator in domain)
            {
                var given = ratings.Where(rating => rating.IndicatorId == indicator.IndicatorId).ToList();
                var cells = new List<string?> { indicator.Name };
                cells.AddRange(Enumerable.Range(0, width).Select(index =>
                    ReportText.Number(given.Count(rating => position.TryGetValue(rating.PointId, out var at) && at == index))));
                cells.Add(ReportText.Number(roster.Count(pupil => given.All(rating => rating.PupilId != pupil))));
                rows.Add(new(ReportRowKind.Data, cells));
            }
        }

        return Result.Success(Report(
            context,
            "Development domain summary",
            filterLines,
            columns,
            rows,
            [$"Out of the {ReportText.Number(roster.Count)} pupils enrolled in the class during the term."],
            width > 4 ? ReportOrientation.Landscape : ReportOrientation.Portrait));
    }
}
