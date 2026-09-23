using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Weekly;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Weekly report sheets (spec 6.10.11): the arm grid, sparse save, publication, the auto-publish setting, one pupil's
/// term, and the two reports of 6.10.12. The parent-facing pages live on the server-rendered portal, not here.
/// </summary>
/// <remarks>
/// The grid PUT deliberately carries the default rate limit rather than the sensitive one: autosave fires on every cell
/// blur and every thirty seconds (spec 6.10.7), which a 10-per-minute policy would throttle mid-sentence.
/// </remarks>
public sealed class WeeklyReportEndpoints : IEndpointModule
{
    private const string Tag = "Weekly reports";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/terms/{termId:guid}/weeks", async (Guid termId, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetTermWeeksQuery(termId.ToString()), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .WithTags(Tag)
            .RequirePrivilege(Privileges.Weekly.View)
            .WithName("GetTermWeeks")
            .WithSummary("List a term's derived weeks")
            .WithDescription(
                "Spec 6.10.3: weeks are derived from the term's dates, never stored or typed. Week 1 starts on the Monday " +
                "of the week containing the start date; weeks run to the week containing the end date, at most 20. " +
                "School-wide `weekly.view`; an arm-scoped caller reads the same list from the arm grid's `weeks`.")
            .Produces<TermWeekListResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        var arms = endpoints.MapGroup("/arms").WithTags(Tag);
        MapGrid(arms);
        MapSave(arms);
        MapPublication(arms, publish: true);
        MapPublication(arms, publish: false);
        MapSettings(arms);

        endpoints.MapGet("/pupils/{pupilId:guid}/weekly", async (
                Guid pupilId, [FromQuery(Name = "termId")] string termId, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetPupilWeeklyQuery(pupilId.ToString(), termId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .WithTags(Tag)
            .RequirePrivilege(Privileges.Weekly.View, ScopeParameterKind.Pupil, "pupilId")
            .WithName("GetPupilWeekly")
            .WithSummary("Read one pupil's weekly reports for a term")
            .WithDescription(
                "Spec 6.10.11: one row per week of the term, with the five days where a report exists and `days: null` " +
                "where nothing was written. A week that falls outside the term's current dates but holds notes is kept and " +
                "flagged `outsideTerm` (6.10.10). Backs the per-pupil tab.")
            .Produces<PupilWeeklyTermDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        var reports = endpoints.MapGroup("/reports").WithTags(Tag);
        reports.MapGet("/weekly-completion", async (
                [FromQuery(Name = "termId")] string termId, [FromQuery(Name = "weekNumber")] int? weekNumber, ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetWeeklyCompletionQuery(termId, weekNumber), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Report.View)
            .WithName("GetWeeklyCompletionReport")
            .WithSummary("Weekly report completion, per arm per week")
            .WithDescription(
                "Spec 6.10.12: one row per active arm per week of the term (or one week with `weekNumber`): pupils with " +
                "any note, lines filled against lines available (pupils on roll x 5 days x 8 lines), published state, and " +
                "the last edit. Bounded by arms x 20 weeks, so not paginated.")
            .Produces<WeeklyCompletionReportDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        reports.MapGet("/weekly-illness", async ([FromQuery(Name = "termId")] string termId, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetWeeklyIllnessQuery(termId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Report.View)
            .RequirePrivilege(Privileges.Pupil.SafeguardingView)
            .WithName("GetWeeklyIllnessReport")
            .WithSummary("Illness observation summary for a term")
            .WithDescription(
                "Spec 6.10.12: every pupil with `symptomsOfIllness` recorded on two or more days of the term, with the " +
                "dates and the text. Health observation about a child, so it needs `report.view` AND " +
                "`pupil.safeguarding.view`, both school-wide.")
            .Produces<WeeklyIllnessReportDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static void MapGrid(RouteGroupBuilder group) =>
        group.MapGet("/{armId:guid}/weekly", async (
                Guid armId, [FromQuery(Name = "termId")] string termId, [FromQuery(Name = "weekNumber")] int? weekNumber, ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetWeeklyGridQuery(armId.ToString(), termId, weekNumber), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Weekly.View, ScopeParameterKind.Arm, "armId")
            .WithName("GetWeeklyGrid")
            .WithSummary("Read one arm's weekly grid for one week")
            .WithDescription(
                "Spec 6.10.7/6.10.11: every active pupil (plus any pupil with notes in this week who has since left) by " +
                "five weekdays, each day carrying all eight lines, in one response. Without `weekNumber` it opens the week " +
                "containing today (Lagos). Also returns the term's week list with publication state (`weeks`), the arm's " +
                "auto-publish option, and the caller's own phrase memory per line (`phrases`). `illnessDays` of 2 or more " +
                "is the quiet marker of 6.10.6.")
            .Produces<WeeklyGridDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSave(RouteGroupBuilder group) =>
        group.MapPut("/{armId:guid}/weekly", async (Guid armId, SaveWeeklyNotesCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { ArmId = armId.ToString() }, cancellationToken);
                return result.Match(TypedResults.NoContent);
            })
            .RequirePrivilege(Privileges.Weekly.Enter, ScopeParameterKind.Arm, "armId")
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .WithName("SaveWeeklyNotes")
            .WithSummary("Save weekly notes, sparse")
            .WithDescription(
                "Spec 6.10.11: bulk upsert of one week's grid in one transaction. Only the cells sent are touched, so a " +
                "Fill down writes only what it filled; a null or blank `value` clears a line. The first note for a pupil " +
                "creates their report with its five days. Last write wins (6.10.10), no version. Nothing is required and " +
                "nothing is scored. 422 for a line over its limit (300, or 500 for the two comments) or a pupil neither on " +
                "the roster nor holding notes in this arm's week; 409 `weekly.term_closed` / `weekly.session_closed`.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapPublication(RouteGroupBuilder group, bool publish) =>
        group.MapPost(publish ? "/{armId:guid}/weekly/{weekNumber:int}/publish" : "/{armId:guid}/weekly/{weekNumber:int}/unpublish", async (
                Guid armId, int weekNumber, WeeklyPublicationRequest body, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(
                    new SetWeeklyPublicationCommand(armId.ToString(), weekNumber, body.TermId, publish), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Weekly.Publish, ScopeParameterKind.Arm, "armId")
            .RequireCsrfToken()
            .WithName(publish ? "PublishWeeklyWeek" : "UnpublishWeeklyWeek")
            .WithSummary(publish ? "Publish one arm's week to parents" : "Unpublish one arm's week")
            .WithDescription(publish
                ? "Spec 6.10.8: per arm per week, no approval chain. Every pupil's report for the week becomes visible on " +
                  "the portal, and a report created later in a published week starts published. 409 " +
                  "`weekly.nothing_to_publish` when no pupil has any note. Idempotent."
                : "Spec 6.10.8: hides the week from parents again. No reason required. Idempotent.")
            .Produces<WeeklyWeekSummaryDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSettings(RouteGroupBuilder group) =>
        group.MapPut("/{armId:guid}/weekly/settings", async (
                Guid armId, UpdateWeeklySettingsCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { ArmId = armId.ToString() }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Weekly.Publish, ScopeParameterKind.Arm, "armId")
            .RequireCsrfToken()
            .WithName("UpdateWeeklySettings")
            .WithSummary("Turn weekly auto-publish on or off for an arm")
            .WithDescription(
                "Spec 6.10.8: off by default. When on, each week with at least one note is published automatically at " +
                "17:00 Lagos time on its Friday (caught up until Sunday if the server was down). A week the teacher " +
                "unpublishes afterwards stays unpublished.")
            .Produces<WeeklySettingsDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
