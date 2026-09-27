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

/// <summary><c>termId</c>, <c>armId</c> or <c>levelId</c>.</summary>
internal sealed record GradeDistributionParameters(
    [FromQuery(Name = "termId")] string? TermId,
    [FromQuery(Name = "armId")] string? ArmId,
    [FromQuery(Name = "levelId")] string? LevelId) : IReportParameters<GradeDistributionFilters>
{
    public GradeDistributionFilters ToFilters() => new(TermId, ArmId, LevelId);
}

/// <summary><c>termId</c>, <c>levelId</c>.</summary>
internal sealed record SubjectPerformanceParameters(
    [FromQuery(Name = "termId")] string? TermId,
    [FromQuery(Name = "levelId")] string? LevelId) : IReportParameters<SubjectPerformanceFilters>
{
    public SubjectPerformanceFilters ToFilters() => new(TermId, LevelId);
}

/// <summary><c>termId</c>, <c>armId</c>.</summary>
internal sealed record DevelopmentSummaryParameters(
    [FromQuery(Name = "termId")] string? TermId,
    [FromQuery(Name = "armId")] string? ArmId) : IReportParameters<DevelopmentSummaryFilters>
{
    public DevelopmentSummaryFilters ToFilters() => new(TermId, ArmId);
}

/// <summary><c>termId</c>, <c>levelId</c>.</summary>
internal sealed record FeeNoticeAuditParameters(
    [FromQuery(Name = "termId")] string? TermId,
    [FromQuery(Name = "levelId")] string? LevelId) : IReportParameters<FeeNoticeAuditFilters>
{
    public FeeNoticeAuditFilters ToFilters() => new(TermId, LevelId);
}

/// <summary><c>sessionId</c>, <c>armId</c> or <c>levelId</c>.</summary>
internal sealed record AnnualCumulativeParameters(
    [FromQuery(Name = "sessionId")] string? SessionId,
    [FromQuery(Name = "armId")] string? ArmId,
    [FromQuery(Name = "levelId")] string? LevelId) : IReportParameters<AnnualCumulativeFilters>
{
    public AnnualCumulativeFilters ToFilters() => new(SessionId, ArmId, LevelId);
}

/// <summary><c>sessionId</c>, <c>levelId</c>, <c>outcome</c>.</summary>
internal sealed record PromotionListParameters(
    [FromQuery(Name = "sessionId")] string? SessionId,
    [FromQuery(Name = "levelId")] string? LevelId,
    [FromQuery(Name = "outcome")] string? Outcome) : IReportParameters<PromotionListFilters>
{
    public PromotionListFilters ToFilters() => new(SessionId, LevelId, Outcome);
}

/// <summary><c>pupilId</c>.</summary>
internal sealed record PupilRecordParameters([FromQuery(Name = "pupilId")] string? PupilId) : IReportParameters<PupilRecordFilters>
{
    public PupilRecordFilters ToFilters() => new(PupilId);
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
        Map<GradeDistributionParameters, GradeDistributionFilters>(
            group, "grade-distribution", "GradeDistribution", "Grade distribution",
            "Counts and percentages of pupils in each grade band, per subject and overall (the term grade), for one arm " +
            "(`armId`) or a level (`levelId`). Every band appears, zero counts included; a text bar (#, 5% each) survives " +
            "photocopying. `report.view`.");
        Map<SubjectPerformanceParameters, SubjectPerformanceFilters>(
            group, "subject-performance", "SubjectPerformance", "Subject performance",
            "Per subject, each arm of the level side by side: average, highest, lowest, counted, absent from the exam, passing " +
            "and pass rate, then the level as a whole. `levelId` is required. `report.view`. PDF prints landscape.");
        Map<DevelopmentSummaryParameters, DevelopmentSummaryFilters>(
            group, "development-summary", "DevelopmentSummary", "Development domain summary",
            "Nursery only (spec 15 section 10.2): per indicator, grouped by domain, how many pupils sit at each rating point, " +
            "and how many are not rated. An arm whose section rates traits returns no rows and a note. `report.view`.");
        Map<FeeNoticeAuditParameters, FeeNoticeAuditFilters>(
            group, "fee-notice-audit", "FeeNoticeAudit", "Fee notice audit",
            "Spec 15 section 10.2: per level, the configured fee lines and amounts for the term, their total, and how many " +
            "pupils carry a typed outstanding figure and its total. Amounts are the notice as configured now (a published " +
            "sheet printed its frozen copy). `levelId` optional. `report.view`.");
        Map<AnnualCumulativeParameters, AnnualCumulativeFilters>(
            group, "annual-cumulative", "AnnualCumulative", "Annual cumulative report",
            "Per pupil for one arm (`armId`) or a level (`levelId`) of the session: the three term averages, cumulative " +
            "average and grade, annual position, and promotion status (the committed decision, or the proposal marked as such " +
            "until promotion runs). Empty with a note until annual computation has run. `report.view`. PDF landscape.");
        Map<PromotionListParameters, PromotionListFilters>(
            group, "promotion-list", "PromotionList", "Promotion list",
            "Per pupil of the session (`levelId` optional): class, annual average, core subject results against the pass " +
            "mark, proposed and final outcome, target class, and the override reason. `outcome` filters on the final outcome, " +
            "or the proposal before promotion runs. Core subjects and pass mark are the result rules as configured now. " +
            "`report.view`. PDF landscape.");
        Map<PupilRecordParameters, PupilRecordFilters>(
            group, "pupil-record", "PupilRecord", "Pupil cumulative record",
            "One pupil across every session: term by term class, average, grade, class and level position and publication " +
            "status, each session closed by its annual result and promotion outcome. An arm-restricted holder sees only the " +
            "terms spent in their arms (403 when none). `report.view`.");
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
