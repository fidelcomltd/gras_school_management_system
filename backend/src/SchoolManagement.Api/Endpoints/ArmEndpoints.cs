using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Classes;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Arms — per-session rooms under a class level (TASK-0039; spec 6.4.3, 6.4.5, 6.4.6, 6.4.7, 6.4.8,
/// 6.4.9). Roster, subjects in effect, result-set states and the transfer log (spec 6.4.5), and
/// <c>POST /arms/{id}/transfer-in</c> (spec 6.4.4), are out of scope — pupils, enrolments, subject
/// mappings and results do not exist in this codebase yet.
/// </summary>
public sealed class ArmEndpoints : IEndpointModule
{
    private const string Tag = "Arms";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/arms")
            .WithTags(Tag);

        MapList(group);
        MapNextLabel(group);
        MapCreate(group);
        MapBulkCreate(group);
        MapGet(group);
        MapUpdate(group);
        MapDelete(group);
    }

    private static void MapList(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] string? cursor = null,
                [FromQuery] int? pageSize = null,
                [FromQuery] string? sessionId = null,
                [FromQuery] string? levelId = null,
                [FromQuery] string? label = null,
                [FromQuery] ArmStatus? status = null,
                [FromQuery] string? formTeacherAdminId = null) =>
            {
                var query = new ListArmsQuery(cursor, pageSize, sessionId, levelId, label, status, formTeacherAdminId);
                var result = await sender.SendAsync(query, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Arm.View)
            .WithName("ListArms")
            .WithSummary("List arms")
            .WithDescription(
                "Spec 6.4.5: filtered by session, level, label (substring match against the composed " +
                "display name), status and form teacher. Sorted by the owning level's chain order, " +
                "then label collated naturally — not alphabetical. Cursor-paginated per spec 9.5. " +
                $"`pageSize` defaults to {CursorPageRequest.DefaultPageSize} and is capped at " +
                $"{CursorPageRequest.MaxPageSize}.")
            .Produces<CursorPage<ArmDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapNextLabel(RouteGroupBuilder group) =>
        group.MapGet("/next-label", async (
                [FromQuery] string levelId,
                [FromQuery] string sessionId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new NextArmLabelQuery(levelId, sessionId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Arm.View)
            .WithName("GetNextArmLabel")
            .WithSummary("Suggest the next unused arm label")
            .WithDescription("Spec 6.4.3: A if the level has no arm this session, B if it has A, and so on. Pre-filled and editable, not reserved.")
            .Produces<NextArmLabelResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapCreate(RouteGroupBuilder group) =>
        group.MapPost(string.Empty, async (
                CreateArmCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(response => TypedResults.Created($"/api/v1/arms/{response.Id}", response));
            })
            .RequirePrivilege(Privileges.Arm.Create)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CreateArm")
            .WithSummary("Create an arm")
            .WithDescription(
                "Spec 6.4.3, 6.4.4: the level must be active and the session upcoming or active. " +
                "Label must be unique within the level and session, case-insensitive. `Idempotency-Key` is REQUIRED.")
            .Produces<ArmDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapBulkCreate(RouteGroupBuilder group) =>
        group.MapPost("/bulk", async (
                BulkCreateArmsCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Arm.Create)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("BulkCreateArms")
            .WithSummary("Create arms for a session, level by level")
            .WithDescription(
                "Spec 6.4.3: \"Create arms for session\" — one level entry per row, each carrying an " +
                "arm count and a capacity. Labels continue from each level's highest existing label. " +
                "Levels given zero arms are skipped. `dryRun` returns the preview without writing. " +
                "The whole run is one transaction. `Idempotency-Key` is REQUIRED.")
            .Produces<BulkCreateArmsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapGet(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetArmQuery(id), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Arm.View)
            .WithName("GetArm")
            .WithSummary("Read one arm")
            .WithDescription("Roster, subjects in effect, result-set states and the transfer log (spec 6.4.5) are out of scope — those need pupils, enrolments, subject mappings and results.")
            .Produces<ArmDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdate(RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateArmCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { Id = id }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Arm.Update)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("UpdateArm")
            .WithSummary("Edit an arm")
            .WithDescription(
                "Spec 6.4.3, 6.4.7: label, capacity, form teacher, status. Every field is " +
                "independently optional; an absent field is left unchanged. Rejected outright " +
                "against a closed arm. Setting `formTeacherAdminId` ADDITIONALLY requires " +
                "`arm.formteacher.assign`, beyond the `arm.update` this route requires.")
            .Produces<ArmDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapDelete(RouteGroupBuilder group) =>
        group.MapDelete("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new DeleteArmCommand(id), cancellationToken);
                return result.Match(TypedResults.NoContent);
            })
            .RequirePrivilege(Privileges.Arm.Delete)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("DeleteArm")
            .WithSummary("Delete an arm")
            .WithDescription("Spec 6.4.7: permitted only where no enrolment has ever existed.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
