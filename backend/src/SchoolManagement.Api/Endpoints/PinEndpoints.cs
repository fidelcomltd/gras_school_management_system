using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Pins;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Access pin batches and pins (spec 6.8.13). Every route is school-wide. No route ever returns a pin value; the
/// print run (a later change) is the only place one is revealed.
/// </summary>
public sealed class PinEndpoints : IEndpointModule
{
    private const string Tag = "Pins";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var batchGroup = endpoints.MapGroup("/pin-batches").WithTags(Tag);
        MapList(batchGroup);
        MapGenerate(batchGroup);
        MapGet(batchGroup);
        MapMarkDistributed(batchGroup);
        MapRevokeBatch(batchGroup);
        MapPrint(batchGroup);
        MapDistributionList(batchGroup);

        var pinGroup = endpoints.MapGroup("/pins").WithTags(Tag);
        MapRevokePin(pinGroup);
        MapReinstatePin(pinGroup);
    }

    private static void MapList(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] Guid? sessionId = null,
                [FromQuery] PinBatchState? state = null,
                [FromQuery] string? cursor = null,
                [FromQuery] int? pageSize = null) =>
            {
                var result = await sender.SendAsync(new ListPinBatchesQuery(sessionId, state, cursor, pageSize), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Pin.View)
            .WithName("ListPinBatches")
            .WithSummary("List pin batches")
            .WithDescription("Newest first, cursor-paged, with per-batch pin counts (spec 6.8.11). Optional `sessionId` and `state` filters.")
            .Produces<CursorPage<PinBatchDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapGenerate(RouteGroupBuilder group) =>
        group.MapPost(string.Empty, async (
                GeneratePinBatchCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(batch => TypedResults.Created($"/api/v1/pin-batches/{batch.Id}", batch));
            })
            .RequirePrivilege(Privileges.Pin.Generate)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("GeneratePinBatch")
            .WithSummary("Generate a pin batch")
            .WithDescription(
                "Spec 6.8.9. Pins are not tied to any pupil: any valid pin opens any registration number's published results. " +
                "`pinCount` 1 to 2000; `pinLength` 10 to 16 (default 10); `maxUses` 1 to 100 (default 3). Above 10 uses, " +
                "`confirmMaxUses` must repeat the number (spec 6.8.12). `name` defaults to the session and current term plus a " +
                "sequence and must be unique in the session (409 `pin_batch.name_taken`). 409 `pin_batch.session_closed` for a " +
                "closed session. `Idempotency-Key` is REQUIRED (spec 9.8.2).")
            .Produces<PinBatchDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapGet(RouteGroupBuilder group) =>
        group.MapGet("/{batchId:guid}", async (Guid batchId, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetPinBatchQuery(batchId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Pin.View)
            .WithName("GetPinBatch")
            .WithSummary("Read a pin batch")
            .WithDescription("The batch with every pin's prefix, state, use count and distinct pupil count. Never a pin value (spec 6.8.13).")
            .Produces<PinBatchDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapMarkDistributed(RouteGroupBuilder group) =>
        group.MapPost("/{batchId:guid}/mark-distributed", async (Guid batchId, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new MarkPinBatchDistributedCommand(batchId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Pin.Generate)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .WithName("MarkPinBatchDistributed")
            .WithSummary("Mark a pin batch distributed")
            .WithDescription("Generated or printed to active (spec 6.8.9 step 8). 409 `pin_batch.not_distributable` from any other state.")
            .Produces<PinBatchDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapRevokeBatch(RouteGroupBuilder group) =>
        group.MapPost("/{batchId:guid}/revoke", async (Guid batchId, PinReasonRequest body, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new RevokePinBatchCommand(batchId, body.Reason), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Pin.Revoke)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("RevokePinBatch")
            .WithSummary("Revoke a pin batch")
            .WithDescription("Revokes the batch and every pin in it in one transaction (spec 6.8.10). Reason required. 409 `pin_batch.already_revoked`.")
            .Produces<PinBatchDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapPrint(RouteGroupBuilder group) =>
        group.MapGet("/{batchId:guid}/print", async (Guid batchId, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new PrintPinBatchCommand(batchId), cancellationToken);
                return result.Match(file => TypedResults.File(file.Content.ToArray(), "application/pdf", file.FileName));
            })
            .RequirePrivilege(Privileges.Pin.Print)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("PrintPinBatch")
            .WithSummary("Print a pin batch's slips")
            .WithDescription(
                "Spec 6.8.9 step 6: a PDF of every non-revoked pin, four slips to an A4 page with cut lines, the only place pin " +
                "values are ever revealed. Moves the batch from generated to printed and writes an audit event, so fetch it and " +
                "save the blob; never open it by top-level navigation. 410 `pin_batch.plaintext_purged` after the purge date; " +
                "409 `pin_batch.revoked`.")
            .Produces<Stream>(StatusCodes.Status200OK, "application/pdf")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapDistributionList(RouteGroupBuilder group) =>
        group.MapGet("/{batchId:guid}/distribution-list", async (Guid batchId, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetPinDistributionListQuery(batchId), cancellationToken);
                return result.Match(file => TypedResults.File(file.Content.ToArray(), "application/pdf", file.FileName));
            })
            .RequirePrivilege(Privileges.Pin.View)
            .WithName("GetPinDistributionList")
            .WithSummary("Download a pin batch's distribution list")
            .WithDescription(
                "Spec 6.8.9 step 7: a numbered sheet of each slip's four-character prefix, with blank columns for pupil name, " +
                "class, guardian name and signature, filled in by hand. No pin values, so it stays available after the purge.")
            .Produces<Stream>(StatusCodes.Status200OK, "application/pdf")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapRevokePin(RouteGroupBuilder group) =>
        group.MapPost("/{pinId:guid}/revoke", async (Guid pinId, PinReasonRequest body, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new RevokePinCommand(pinId, body.Reason), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Pin.Revoke)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .WithName("RevokePin")
            .WithSummary("Revoke one pin")
            .WithDescription("Reason required. 409 `pin.already_revoked`.")
            .Produces<PinSummaryDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapReinstatePin(RouteGroupBuilder group) =>
        group.MapPost("/{pinId:guid}/reinstate", async (Guid pinId, PinReasonRequest body, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new ReinstatePinCommand(pinId, body.Reason), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Pin.Revoke)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .WithName("ReinstatePin")
            .WithSummary("Reinstate a suspended pin")
            .WithDescription("Clears a spread-control suspension (spec 6.8.13); reason required. 409 `pin.not_suspended` otherwise.")
            .Produces<PinSummaryDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
