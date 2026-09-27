using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Promotion;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>End-of-session promotion (spec 6.3.7).</summary>
public sealed class PromotionEndpoints : IEndpointModule
{
    private const string Tag = "Promotion";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // The batch-returning endpoints first: PromotionBatchDto must reach the schema generator as a type (with its example)
        // before it is met as the preview's nullable committedBatch property, whose example is null.
        var sessions = endpoints.MapGroup("/sessions").WithTags(Tag);
        MapCommit(sessions);
        MapReverse(endpoints.MapGroup("/promotion-batches").WithTags(Tag));
        MapPreview(sessions);
    }

    private static void MapPreview(RouteGroupBuilder group) =>
        group.MapGet("/{sessionId:guid}/promotion/preview", async (
                Guid sessionId,
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] Guid? targetSessionId = null) =>
            {
                var result = await sender.SendAsync(new GetPromotionPreviewQuery(sessionId, targetSessionId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Promotion.Run, ScopeParameterKind.None)
            .WithName("GetPromotionPreview")
            .WithSummary("Preview a session's promotion")
            .WithDescription(
                "Spec 6.3.7's review screen: one row per active pupil with the annual average, core subject results, the " +
                "proposed outcome (Promoted, Repeat, or Graduated at the terminal level; blank with no annual result) and a " +
                "default target arm dealt round-robin by descending average; the target session's arms with capacity and " +
                "current counts; pupils no longer active, listed apart; and every unmet precondition in `blockers`. " +
                "`targetSessionId` defaults to the next session by start date. When promotion has already run, `rows` is empty " +
                "and `committedBatch` describes the batch.")
            .Produces<PromotionPreviewDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapCommit(RouteGroupBuilder group) =>
        group.MapPost("/{sessionId:guid}/promotion", async (
                Guid sessionId,
                CommitPromotionCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { SessionId = sessionId }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Promotion.Run, ScopeParameterKind.None)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CommitPromotion")
            .WithSummary("Commit a session's promotion")
            .WithDescription(
                "Spec 6.3.7: one decision per active pupil, exactly once, applied in ONE transaction: each current enrolment " +
                "closes on the old session's end date and a new one opens in the target arm on the new session's start date; " +
                "a graduate changes status and gets no enrolment. Changing a proposed outcome, or choosing `PromotedOnTrial` " +
                "(which also needs a reason of 10 to 500 characters), needs `promotion.decide`. Over capacity is allowed " +
                "(spec 6.4.9 warns, never blocks). 409 while any preview blocker remains or when the pupils changed since the " +
                "preview. `Idempotency-Key` is REQUIRED.")
            .Produces<PromotionBatchDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapReverse(RouteGroupBuilder group) =>
        group.MapPost("/{batchId:guid}/reverse", async (
                Guid batchId,
                ReversePromotionCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { BatchId = batchId }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Promotion.Reverse, ScopeParameterKind.None)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("ReversePromotion")
            .WithSummary("Reverse a promotion")
            .WithDescription(
                "Spec 6.3.7: removes the enrolments the batch opened, reopens the ones it closed, restores graduates to " +
                "active and marks the batch reversed (the row is kept). Refused with 409 once a mark has been entered or a " +
                "pin used in the new session, or when a pupil has moved since. Needs a reason of 10 to 500 characters. " +
                "`Idempotency-Key` is accepted, not required.")
            .Produces<PromotionBatchDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
