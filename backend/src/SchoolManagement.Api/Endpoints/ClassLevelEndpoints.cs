using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Classes;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Class levels — year groups and the progression chain (TASK-0038; spec 6.4.1, 6.4.2, 6.4.7, 6.4.8,
/// 6.4.9). Arms (creation, capacity, the <c>open</c>-term precondition, session arm counts) are
/// <see cref="ArmEndpoints"/> — TASK-0039. No arm count is carried on any DTO here; a level's own
/// arms are reachable via <c>GET /arms?levelId=</c>.
/// </summary>
public sealed class ClassLevelEndpoints : IEndpointModule
{
    private const string Tag = "Levels";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/levels")
            .WithTags(Tag);

        MapList(group);
        MapCreate(group);
        MapReorder(group);
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
                [FromQuery] string? status = null) =>
            {
                var includeInactive = string.Equals(status, "all", StringComparison.OrdinalIgnoreCase);
                var result = await sender.SendAsync(new ListLevelsQuery(cursor, pageSize, includeInactive), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Level.View)
            .WithName("ListLevels")
            .WithSummary("List class levels")
            .WithDescription(
                "Spec 6.4.9: active by default, ordered by progressionOrder; `?status=all` also " +
                "returns inactive levels. Cursor-paginated per spec 9.5. " +
                $"`pageSize` defaults to {CursorPageRequest.DefaultPageSize} and is capped at " +
                $"{CursorPageRequest.MaxPageSize}.")
            .Produces<CursorPage<LevelDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapCreate(RouteGroupBuilder group) =>
        group.MapPost(string.Empty, async (
                CreateLevelCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(response => TypedResults.Created($"/api/v1/levels/{response.Id}", response));
            })
            .RequirePrivilege(Privileges.Level.Create)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CreateLevel")
            .WithSummary("Create a class level")
            .WithDescription(
                "Spec 6.4.2: give `insertAfterLevelId` and the server rewires the chain and reorders " +
                "in one transaction (the worked case), or give `progressionOrder` directly. Reruns " +
                "the eight chain rules before committing. `Idempotency-Key` is REQUIRED.")
            .Produces<LevelDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapReorder(RouteGroupBuilder group) =>
        group.MapPost("/reorder", async (
                ReorderLevelsCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Level.Update)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("ReorderLevels")
            .WithSummary("Reorder every active class level")
            .WithDescription(
                "Spec 6.4.2, 6.4.9: whole ordered array of active level ids. Atomic — rewrites " +
                "progressionOrder and infers nextLevelId from adjacency. Used by the drag-and-drop " +
                "list.")
            .Produces<IReadOnlyList<LevelDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapGet(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetLevelQuery(id), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Level.View)
            .WithName("GetLevel")
            .WithSummary("Read one class level")
            .WithDescription("No arm counts — that needs Arm, TASK-0039.")
            .Produces<LevelDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdate(RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateLevelCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { Id = id }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Level.Update)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("UpdateLevel")
            .WithSummary("Edit a class level")
            .WithDescription(
                "Spec 6.4.2: name, section, next level, order, status. Every field is independently " +
                "optional; an absent field is left unchanged. An empty string for `nextLevelId` " +
                "clears it (graduating candidate). Reruns the eight chain rules before committing. " +
                "Changing `status` ADDITIONALLY requires `level.deactivate`, beyond the `level.update` " +
                "this whole route requires.")
            .Produces<LevelDto>(StatusCodes.Status200OK)
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
                var result = await sender.SendAsync(new DeleteLevelCommand(id), cancellationToken);
                return result.Match(TypedResults.NoContent);
            })
            .RequirePrivilege(Privileges.Level.Delete)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("DeleteLevel")
            .WithSummary("Delete a class level")
            .WithDescription(
                "Spec 6.4.2: permitted only where nothing has ever referenced the level. Checks the " +
                "two references this codebase can see today (another level's nextLevelId, and any " +
                "arm under this level); enrolment, subject-mapping and result references remain " +
                "DEFERRED — those tables do not exist yet.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
