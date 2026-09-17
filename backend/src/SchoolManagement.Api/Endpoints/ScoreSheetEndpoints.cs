using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// The score sheet — one arm's marks for one subject and term (TASK-0076 dispatch B; spec 6.7.4).
/// Maps routes under the same <c>/arms</c> prefix <see cref="ArmEndpoints"/> owns — a domain split,
/// not a URL-prefix one, the same convention <see cref="SubjectEndpoints"/> already uses.
/// </summary>
/// <remarks>
/// <para>
/// ROUTE SHAPE DEPARTS FROM spec 6.7.13's <c>?arm_id=</c> query form, ON PURPOSE (approved contract
/// delta, 2026-09-17): <c>PrivilegeAuthorizationHandler</c> resolves scope from route VALUES only
/// (<c>PrivilegeAuthorizationHandler.cs:91</c>), so an arm-scoped grant cannot be checked against a
/// query parameter. Precedent: <c>GET /arms/{id}/subjects</c>.
/// </para>
/// <para>
/// <c>GetScoreSheet</c>/<c>SaveScoreSheet</c>/<c>VoidScoreSheet</c> are declaratively scoped to the
/// arm named in the path (<c>result.view</c>/<c>result.score.enter</c>/<c>result.score.void</c>) — the
/// DATA-DEPENDENT rest (result-set state, term/session closure, per-cell validation, staleness) is the
/// handler's job, the same split every other data-dependent route in this codebase uses.
/// </para>
/// </remarks>
public sealed class ScoreSheetEndpoints : IEndpointModule
{
    private const string Tag = "Score sheets";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/arms").WithTags(Tag);

        MapGetScoreSheet(group);
        MapSaveScoreSheet(group);
        MapVoidScoreSheet(group);
    }

    private static void MapGetScoreSheet(RouteGroupBuilder group) =>
        group.MapGet("/{armId:guid}/score-sheets", async (
                Guid armId,
                [FromQuery(Name = "subjectId")] string subjectId,
                [FromQuery(Name = "termId")] string termId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(
                    new GetScoreSheetQuery(armId.ToString(), subjectId, termId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.View, ScopeParameterKind.Arm, "armId")
            .WithName("GetScoreSheet")
            .WithSummary("Read one arm's score sheet for a subject and term")
            .WithDescription(
                "Spec 6.7.4: every active pupil in the arm as a row, including one with no marks " +
                "entered at all. `version` is null before any row exists; send it back unchanged on " +
                "`PUT` to detect a concurrent edit.")
            .Produces<ScoreSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSaveScoreSheet(RouteGroupBuilder group) =>
        group.MapPut("/{armId:guid}/score-sheets", async (
                Guid armId,
                SaveScoreSheetCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { ArmId = armId.ToString() }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.ScoreEnter, ScopeParameterKind.Arm, "armId")
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("SaveScoreSheet")
            .WithSummary("Save a whole score sheet")
            .WithDescription(
                "Spec 6.7.4: whole-sheet save, one transaction; a single invalid cell rejects the " +
                "whole save. The first save for an arm and term creates its result set (Draft). A " +
                "row with every cell blank and not absent is not stored, and deletes an existing " +
                "row. `Idempotency-Key` is ACCEPTED, not required — a retry otherwise 409s on its " +
                "own stale `version`.")
            .Produces<ScoreSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapVoidScoreSheet(RouteGroupBuilder group) =>
        group.MapPost("/{armId:guid}/score-sheets/void", async (
                Guid armId,
                VoidScoreSheetCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { ArmId = armId.ToString() }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.ScoreVoid, ScopeParameterKind.Arm, "armId")
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("VoidScoreSheet")
            .WithSummary("Void every mark for a subject, arm and term")
            .WithDescription(
                "Spec 6.7.4: a reason of at least ten characters is required. Voided rows are kept, " +
                "excluded from computation, and vanish from `GET`. `Idempotency-Key` is ACCEPTED, " +
                "not required.")
            .Produces<VoidScoreSheetResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
