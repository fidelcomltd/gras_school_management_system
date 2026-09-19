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
/// The primary trait-rating grid — one arm's affective and psychomotor ratings for one term
/// (TASK-0083 stage 1; spec §6.7.7, §6.7.12 amendment). Maps routes under the same <c>/arms</c> prefix
/// <see cref="ScoreSheetEndpoints"/> owns — a domain split, not a URL-prefix one.
/// </summary>
/// <remarks>
/// <para>
/// ROUTE SHAPE IS ARM-SCOPED, NOT RESULT-SET-SCOPED — departing from spec §6.7.13's literal
/// <c>PUT /result-sets/{id}/traits</c>, on purpose (TASK-0083 stage 0's approved contract delta),
/// for the SAME reason TASK-0076 departed for score sheets: a class teacher must be able to save the
/// FIRST rating before any <c>result_set</c> row exists ("Not started"). A result-set-scoped path
/// needs an id that does not exist yet. <c>PrivilegeAuthorizationHandler</c> also resolves scope from
/// route VALUES only, so an arm-scoped grant cannot be checked against a body or query field.
/// </para>
/// <para>
/// <c>GetTraitRatings</c>/<c>SaveTraitRatings</c> are declaratively scoped to the arm named in the
/// path (<c>result.view</c>/<c>result.trait.enter</c>) — the DATA-DEPENDENT rest (result-set state,
/// term/session closure, ruling R1's section gate, per-cell validation, staleness) is the handler's
/// job, the same split every other data-dependent route in this module uses.
/// </para>
/// </remarks>
public sealed class TraitRatingEndpoints : IEndpointModule
{
    private const string Tag = "Trait ratings";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/arms").WithTags(Tag);

        MapGetTraitRatings(group);
        MapSaveTraitRatings(group);
    }

    private static void MapGetTraitRatings(RouteGroupBuilder group) =>
        group.MapGet("/{armId:guid}/trait-ratings", async (
                Guid armId,
                [FromQuery(Name = "termId")] string termId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetTraitRatingsQuery(armId.ToString(), termId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.View, ScopeParameterKind.Arm, "armId")
            .WithName("GetTraitRatings")
            .WithSummary("Read one arm's trait-rating grid for a term")
            .WithDescription(
                "Spec 6.7.7/6.7.12 amendment: every active pupil in the arm as a row, across the " +
                "affective and psychomotor blocks. `version` is null before any rating exists; send " +
                "it back unchanged on `PUT` to detect a concurrent edit. 422 " +
                "`trait_ratings.section_not_rated` when the arm's section does not rate traits (ruling R1).")
            .Produces<TraitRatingSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSaveTraitRatings(RouteGroupBuilder group) =>
        group.MapPut("/{armId:guid}/trait-ratings", async (
                Guid armId,
                SaveTraitRatingsCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { ArmId = armId.ToString() }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.TraitEnter, ScopeParameterKind.Arm, "armId")
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("SaveTraitRatings")
            .WithSummary("Save trait ratings, partial or whole grid")
            .WithDescription(
                "Spec 6.7.7/6.7.12 amendment: partial-save, one transaction (Q1-A ruling — an " +
                "omitted trait key leaves that rating untouched, an explicit null clears it). The " +
                "first rating for an arm and term creates its result set (Draft), never flags " +
                "`needsRecompute` on an EXISTING one — ratings are never computed. Allowed only " +
                "while the result set is Draft or Returned for Correction, else 409 " +
                "`trait_ratings.result_set_locked`. `Idempotency-Key` is ACCEPTED, not required — a " +
                "retry otherwise 409s on its own stale `version`.")
            .Produces<TraitRatingSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
