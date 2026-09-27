using System.Globalization;
using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Reports.Results;

/// <summary>Merit list filters: an arm or a level, and optionally the top N.</summary>
/// <param name="TermId">The term.</param>
/// <param name="ArmId">One arm; or give <paramref name="LevelId"/>.</param>
/// <param name="LevelId">A whole level, ranked by level position.</param>
/// <param name="Top">Only positions up to this one (ties at the cut stay in), 1 to 500.</param>
public sealed record MeritListFilters(string? TermId, string? ArmId, string? LevelId, int? Top) : IReportFilters;

/// <summary>
/// Spec 15 section 10, merit list: position order for an arm or a level (position, name, registration number, average,
/// grade), for prize-giving. Prints portrait in two columns. Unranked pupils are left out.
/// </summary>
internal sealed class MeritListReport(IReportReader reader) : ReportBuilder<MeritListFilters>
{
    public override string Key => "merit-list";

    protected override async Task<Result<ReportDto>> BuildAsync(MeritListFilters filters, ReportContext context, CancellationToken cancellationToken)
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

        if (filters.Top is < 1 or > 500)
        {
            return Result.Failure<ReportDto>(Error.Validation("report.filter", "top must be between 1 and 500."));
        }

        if (armId is { } wanted && !context.Scope.Allows(wanted))
        {
            return Result.Failure<ReportDto>(OutOfScope());
        }

        var term = await reader.FindTermAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<ReportDto>(Error.NotFound("report.not_found", "No term was found with that id."));
        }

        var arms = (await reader.ListArmsAsync(term.SessionId, cancellationToken).ConfigureAwait(false))
            .Where(arm => (armId is null || arm.ArmId == armId) && (levelId is null || arm.LevelId == levelId) && context.Scope.Allows(arm.ArmId))
            .ToDictionary(arm => arm.ArmId);
        if (arms.Count == 0)
        {
            return Result.Failure<ReportDto>(Error.NotFound("report.not_found", "No class you can see matches those filters in that term's session."));
        }

        var sets = (await reader.ListResultSetsAsync(termId, cancellationToken).ConfigureAwait(false))
            .Where(set => arms.ContainsKey(set.ArmId))
            .ToDictionary(set => set.ResultSetId);
        var results = await reader.ListTermResultsAsync(sets.Keys, cancellationToken).ConfigureAwait(false);
        var pupils = await reader.FindPupilsAsync([.. results.Select(result => result.PupilId)], cancellationToken).ConfigureAwait(false);

        var byLevel = levelId is not null;
        var ranked = results
            .Select(result => (Term: result, Position: byLevel ? result.LevelPosition : result.ArmPosition, Tied: byLevel ? result.LevelTied : result.ArmTied))
            .Where(entry => entry.Position is not null && (filters.Top is null || entry.Position <= filters.Top))
            .OrderBy(entry => entry.Position)
            .ThenBy(entry => pupils.GetValueOrDefault(entry.Term.PupilId)?.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var columns = new List<ReportColumnDto> { new("Pos.", ReportAlign.Right), new("Name", ReportAlign.Left), new("Reg. no.", ReportAlign.Left) };
        if (byLevel)
        {
            columns.Add(new("Class", ReportAlign.Left));
        }

        columns.Add(new("Average", ReportAlign.Right));
        columns.Add(new("Grade", ReportAlign.Center));

        var rows = ranked.ConvertAll(entry =>
        {
            var pupil = pupils.GetValueOrDefault(entry.Term.PupilId);
            var cells = new List<string?> { ReportText.Position(entry.Position, entry.Tied), pupil?.DisplayName ?? "Unknown pupil", pupil?.RegistrationNumber };
            if (byLevel)
            {
                cells.Add(arms[sets[entry.Term.ResultSetId].ArmId].Name);
            }

            cells.Add(ReportText.Decimal(entry.Term.Average));
            cells.Add(entry.Term.Grade);
            return new ReportRowDto(ReportRowKind.Data, cells);
        });

        var scopeLine = byLevel ? $"Level: {arms.Values.First().LevelName}" : $"Class: {arms.Values.First().Name}";
        var filterLines = new List<string> { scopeLine, $"{term.Name}, {term.SessionName}" };
        if (filters.Top is { } top)
        {
            filterLines.Add($"Top {top.ToString(CultureInfo.InvariantCulture)}");
        }

        var notes = new List<string>();
        if (byLevel && context.Scope.Arms is not null)
        {
            notes.Add("Only the classes you have access to are listed; positions are across the whole level.");
        }

        return Result.Success(Report(context, "Merit list", filterLines, columns, rows, notes, twoUp: true));
    }
}
