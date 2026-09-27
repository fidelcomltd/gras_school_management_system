using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Fees;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>The next-term fee notice (spec 6.2.13): a printed notice, not a finance module.</summary>
public sealed class FeeNoticeEndpoints : IEndpointModule
{
    private const string Tag = "Fee notice";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var notices = endpoints.MapGroup("/fee-notices").WithTags(Tag);
        MapGetGrid(notices);
        MapSaveGrid(notices);

        var arms = endpoints.MapGroup("/arms").WithTags(Tag);
        MapGetOutstanding(arms);
        MapSaveOutstanding(arms);
    }

    private static void MapGetGrid(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] Guid sectionId,
                [FromQuery] Guid termId) =>
            {
                var result = await sender.SendAsync(new GetFeeNoticeGridQuery(sectionId, termId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Fee.Manage, ScopeParameterKind.None)
            .WithName("GetFeeNoticeGrid")
            .WithSummary("Read a section's fee notice grid for a term")
            .WithDescription(
                "Spec 6.2.13: the section's lines (rows) against its active class levels (columns), with the amounts printed on " +
                "`termId`'s result sheets, which carry what to pay NEXT term. A section that has saved no lines gets the six " +
                "seeded lines with `isDefault: true`, unsaved. `previousTermId` is the term before, for \"copy from previous term\".")
            .Produces<FeeNoticeGridDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSaveGrid(RouteGroupBuilder group) =>
        group.MapPut(string.Empty, async (
                SaveFeeNoticeGridCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Fee.Manage, ScopeParameterKind.None)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("SaveFeeNoticeGrid")
            .WithSummary("Save a section's fee notice grid for a term")
            .WithDescription(
                "The whole grid in one save: the lines as listed become the section's lines in that order (renamed, reordered " +
                "or new); a saved line left out is removed together with its amounts in every term. Cells are this term's " +
                "amounts in naira; a null amount clears the cell and levels not listed are left as they are. At most one line " +
                "is the per-pupil outstanding figure, which takes no amounts and carries the portal switch. Nothing is " +
                "invoiced, receipted or carried forward. `Idempotency-Key` is accepted, not required.")
            .Produces<FeeNoticeGridDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapGetOutstanding(RouteGroupBuilder group) =>
        group.MapGet("/{armId:guid}/outstanding-fees", async (
                Guid armId,
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] Guid termId) =>
            {
                var result = await sender.SendAsync(new GetOutstandingFeesQuery(armId, termId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Fee.Manage, ScopeParameterKind.None)
            .WithName("GetOutstandingFees")
            .WithSummary("Read an arm's outstanding-fee figures for a term")
            .WithDescription(
                "Spec 6.2.13's bulk grid: the arm's active pupils against one outstanding figure each, typed from the school's " +
                "own records. Blank (null) is the normal case and prints as a dash. `locked` is true once the arm's results " +
                "for the term are published.")
            .Produces<OutstandingFeeSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSaveOutstanding(RouteGroupBuilder group) =>
        group.MapPut("/{armId:guid}/outstanding-fees", async (
                Guid armId,
                SaveOutstandingFeesCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { ArmId = armId }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Fee.Manage, ScopeParameterKind.None)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("SaveOutstandingFees")
            .WithSummary("Save an arm's outstanding-fee figures for a term")
            .WithDescription(
                "Saves the listed pupils' figures in naira; a null amount clears one and pupils not listed are left as they " +
                "are. 409 once the arm's results for the term are published (withdraw them to correct a figure). No figure " +
                "ever blocks a result, a pin or a portal lookup. `Idempotency-Key` is accepted, not required.")
            .Produces<OutstandingFeeSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
