using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Reports.Results;

/// <summary>Development domain summary filters.</summary>
/// <param name="TermId">The term.</param>
/// <param name="ArmId">The (nursery) arm.</param>
public sealed record DevelopmentSummaryFilters(string? TermId, string? ArmId) : IReportFilters;

/// <summary>
/// Spec 15 section 10.2, development domain summary (nursery only): for one arm and term, how many pupils sit at each rating
/// point of each indicator, so a head teacher sees at a glance what thirty sheets would say one by one.
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

        var points = await reader.ListRatingPointsAsync([.. indicators.Select(indicator => indicator.RatingScaleId).Distinct()], cancellationToken).ConfigureAwait(false);
        var set = (await reader.ListResultSetsAsync(termId, cancellationToken).ConfigureAwait(false)).FirstOrDefault(candidate => candidate.ArmId == armId);
        var ratings = set is null ? [] : await reader.ListDevelopmentRatingsAsync(set.ResultSetId, cancellationToken).ConfigureAwait(false);
        var rated = ratings.Select(rating => rating.PupilId).Distinct().Count();

        // Columns follow the scale most indicators use; another scale's points line up by their order on it.
        var mainScale = indicators.GroupBy(indicator => indicator.RatingScaleId).OrderByDescending(group => group.Count()).First().Key;
        var headings = points.Where(point => point.RatingScaleId == mainScale).OrderBy(point => point.Order).ToList();
        var position = points.GroupBy(point => point.RatingScaleId)
            .SelectMany(scale => scale.OrderBy(point => point.Order).Select((point, index) => (point.PointId, Index: index)))
            .ToDictionary(entry => entry.PointId, entry => entry.Index);
        var width = Math.Max(headings.Count, points.GroupBy(point => point.RatingScaleId).Max(scale => scale.Count()));

        var columns = new List<ReportColumnDto> { new("Indicator", ReportAlign.Left) };
        columns.AddRange(Enumerable.Range(0, width).Select(index => new ReportColumnDto(index < headings.Count ? headings[index].Label : $"Point {index + 1}", ReportAlign.Right)));
        columns.Add(new("Not rated", ReportAlign.Right));

        var rows = new List<ReportRowDto>();
        foreach (var domain in indicators.GroupBy(indicator => indicator.DomainName))
        {
            rows.Add(new(ReportRowKind.Heading, [domain.Key, .. Enumerable.Repeat<string?>(null, width + 1)]));
            foreach (var indicator in domain)
            {
                var given = ratings.Where(rating => rating.IndicatorId == indicator.IndicatorId).ToList();
                var cells = new List<string?> { indicator.Name };
                cells.AddRange(Enumerable.Range(0, width).Select(index =>
                    ReportText.Number(given.Count(rating => position.TryGetValue(rating.PointId, out var at) && at == index))));
                cells.Add(ReportText.Number(rated - given.Count));
                rows.Add(new(ReportRowKind.Data, cells));
            }
        }

        return Result.Success(Report(
            context,
            "Development domain summary",
            filterLines,
            columns,
            rows,
            [$"Counts among the {ReportText.Number(rated)} pupils with at least one rating this term."],
            width > 4 ? ReportOrientation.Landscape : ReportOrientation.Portrait));
    }
}
