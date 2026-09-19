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
/// The nursery development-rating grid — one arm's development-indicator ratings for one term
/// (TASK-0083 stage 2; spec §6.7.7, §6.7.12 amendment, Appendix E.3). Maps routes under the same
/// <c>/arms</c> prefix <see cref="TraitRatingEndpoints"/> owns — a domain split, not a URL-prefix one.
/// </summary>
/// <remarks>
/// <para>
/// ROUTE SHAPE IS ARM-SCOPED, NOT RESULT-SET-SCOPED — same departure from spec §6.7.13's literal path
/// as <see cref="TraitRatingEndpoints"/>, for the same reason: a class teacher must be able to save
/// the FIRST rating before any <c>result_set</c> row exists ("Not started").
/// </para>
/// <para>
/// <c>GetDevelopmentRatings</c>/<c>SaveDevelopmentRatings</c> are declaratively scoped to the arm named
/// in the path (<c>result.view</c>/<c>result.trait.enter</c> — ruling R3 reuses the trait privilege,
/// spec names no separate one for development ratings). The DATA-DEPENDENT rest (result-set state,
/// term/session closure, ruling R1's section gate, per-cell validation, staleness) is the handler's
/// job, the same split every other data-dependent route in this module uses.
/// </para>
/// </remarks>
public sealed class DevelopmentRatingEndpoints : IEndpointModule
{
    private const string Tag = "Development ratings";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/arms").WithTags(Tag);

        MapGetDevelopmentRatings(group);
        MapSaveDevelopmentRatings(group);
    }

    private static void MapGetDevelopmentRatings(RouteGroupBuilder group) =>
        group.MapGet("/{armId:guid}/development-ratings", async (
                Guid armId,
                [FromQuery(Name = "termId")] string termId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetDevelopmentRatingsQuery(armId.ToString(), termId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.View, ScopeParameterKind.Arm, "armId")
            .WithName("GetDevelopmentRatings")
            .WithSummary("Read one arm's development-rating grid for a term")
            .WithDescription(
                "Spec 6.7.7/6.7.12 amendment, Appendix E.3: every active pupil in the arm as a row, " +
                "across the arm's section's ACTIVE development domains, ordered by display order. " +
                "`activeIndicatorTotal` is the sum an entry screen reads for the >60 warning. " +
                "`version` is null before any rating exists; send it back unchanged on `PUT` to " +
                "detect a concurrent edit. 422 `development_ratings.section_not_rated` when the arm's " +
                "section has no active development domain (ruling R1) — a primary arm, for example.")
            .Produces<DevelopmentRatingSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSaveDevelopmentRatings(RouteGroupBuilder group) =>
        group.MapPut("/{armId:guid}/development-ratings", async (
                Guid armId,
                SaveDevelopmentRatingsCommand command,
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
            .WithName("SaveDevelopmentRatings")
            .WithSummary("Save development-indicator ratings, partial or whole grid")
            .WithDescription(
                "Spec 6.7.7/6.7.12 amendment, Appendix E.3: partial-save, one transaction (Q1-A " +
                "ruling — an omitted indicator key leaves that rating untouched, an explicit null " +
                "clears it). Q3-A: a comment requires a point — 422 " +
                "`request.validation_failed` when one is sent without the other, or over 120 " +
                "characters, or on a domain whose `allowsIndicatorComment` is false; clearing the " +
                "point clears the comment. The first rating for an arm and term creates its result " +
                "set (Draft), never flags `needsRecompute` on an EXISTING one — ratings are never " +
                "computed. Allowed only while the result set is Draft or Returned for Correction, " +
                "else 409 `development_ratings.result_set_locked`. `Idempotency-Key` is ACCEPTED, " +
                "not required — a retry otherwise 409s on its own stale `version`.")
            .Produces<DevelopmentRatingSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
