using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Reports;
using SchoolManagement.Application.Reports.Results;

namespace SchoolManagement.Api.Endpoints;

/// <summary>A report's query string, bound by name, turned into the Application's filters.</summary>
/// <typeparam name="TFilters">The filters.</typeparam>
internal interface IReportParameters<out TFilters>
    where TFilters : IReportFilters
{
    TFilters ToFilters();
}

/// <summary><c>termId</c>, <c>armId</c>.</summary>
internal sealed record BroadsheetParameters(
    [FromQuery(Name = "termId")] string? TermId,
    [FromQuery(Name = "armId")] string? ArmId) : IReportParameters<BroadsheetFilters>
{
    public BroadsheetFilters ToFilters() => new(TermId, ArmId);
}

/// <summary><c>termId</c>, <c>armId</c> or <c>levelId</c>, <c>top</c>.</summary>
internal sealed record MeritListParameters(
    [FromQuery(Name = "termId")] string? TermId,
    [FromQuery(Name = "armId")] string? ArmId,
    [FromQuery(Name = "levelId")] string? LevelId,
    [FromQuery(Name = "top")] int? Top) : IReportParameters<MeritListFilters>
{
    public MeritListFilters ToFilters() => new(TermId, ArmId, LevelId, Top);
}

/// <summary><c>termId</c>, <c>levelId</c>, <c>state</c>.</summary>
internal sealed record ResultEntryProgressParameters(
    [FromQuery(Name = "termId")] string? TermId,
    [FromQuery(Name = "levelId")] string? LevelId,
    [FromQuery(Name = "state")] string? State) : IReportParameters<ResultEntryProgressFilters>
{
    public ResultEntryProgressFilters ToFilters() => new(TermId, LevelId, State);
}

/// <summary>
/// Spec 15 section 10's reports. Each report is two routes: <c>GET /reports/{name}</c> (the table as JSON) and
/// <c>GET /reports/{name}/export?format=csv|pdf</c> (a file, <c>report.export</c>, audited as <c>report.export</c> with the
/// filters and the row count). Every report is the same <see cref="ReportDto"/> shape, so one screen and one exporter serve them all.
/// </summary>
public sealed class ReportEndpoints : IEndpointModule
{
    private const string Tag = "Reports";

    private const string Scope =
        " The privilege is checked in the handler, not on the route (a route with no arm cannot see an arm-restricted grant): " +
        "an arm-restricted holder sees only their arms, and naming another is 403.";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/reports").WithTags(Tag);

        Map<BroadsheetParameters, BroadsheetFilters>(
            group, "broadsheet", "Broadsheet", "Arm broadsheet",
            "One row per pupil, a CA / Exam / Total group per subject, then total, average, grade and level position, by arm " +
            "position. Reads computed results in any state (the head teacher approves from it), with a note until published. " +
            "`report.view`. PDF prints landscape.");
        Map<MeritListParameters, MeritListFilters>(
            group, "merit-list", "MeritList", "Merit list",
            "Position order for one arm (`armId`) or a whole level (`levelId`, ranked by level position): position, name, " +
            "registration number, class for a level, average and grade. `top` keeps positions up to N, ties included. " +
            "Unranked pupils are left out. `report.view`. PDF prints portrait in two columns.");
        Map<ResultEntryProgressParameters, ResultEntryProgressFilters>(
            group, "result-entry-progress", "ResultEntryProgress", "Result entry progress",
            "One row per arm for the term: pupils, subjects mapped, mark cells, ratings, attendance, teacher's and head's " +
            "remarks complete of total, and the result set's state. Filters: `levelId`, `state` (`NotStarted` or a " +
            "result-set state). The same figures as each class's readiness grid. `report.view`.");
    }

    private static void Map<TParameters, TFilters>(RouteGroupBuilder group, string path, string name, string summary, string description)
        where TParameters : IReportParameters<TFilters>
        where TFilters : IReportFilters
    {
        group.MapGet($"/{path}", async ([AsParameters] TParameters parameters, ISender sender, CancellationToken cancellationToken) =>
                (await sender.SendAsync(new GetReportQuery(parameters.ToFilters()), cancellationToken)).Match(TypedResults.Ok))
            .RequireAuthenticatedCaller()
            .WithName($"Get{name}Report")
            .WithSummary(summary)
            .WithDescription(description + Scope)
            .Produces<ReportDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet($"/{path}/export", async (
                [AsParameters] TParameters parameters,
                [FromQuery(Name = "format")] string? format,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new ExportReportCommand(parameters.ToFilters(), format), cancellationToken);
                return result.Match(file => TypedResults.File(file.Content.ToArray(), file.ContentType, file.FileName));
            })
            .RequireAuthenticatedCaller()
            .WithName($"Export{name}Report")
            .WithSummary($"{summary}, as CSV or PDF")
            .WithDescription(
                $"The same report as `GET /reports/{path}`, as `format=csv` (UTF-8 with a byte-order mark, formula-safe) or " +
                "`format=pdf` (A4). Needs `report.export` as well as the report's own privilege, and writes a `report.export` " +
                "audit event naming the report, the filters and the row count." + Scope)
            .Produces<Stream>(StatusCodes.Status200OK, "text/csv", "application/pdf")
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
