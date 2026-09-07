using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Security.Roles;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Role management (TASK-0028 dispatch 2; spec 6.1.4, 6.1.7, 6.1.9, 9.4, 9.5). Approved contract
/// delta: <c>.agent/decisions/2026-Q3-contract-deltas.md</c> entry <c>TASK-0028</c> §2-4. The
/// privilege register (<c>GET /privileges</c>, dispatch 1) and role ASSIGNMENT (TASK-0030) are
/// separate slices.
/// </summary>
/// <remarks>
/// <para>
/// Every route here has ONE fixed privilege and is gated declaratively via
/// <c>RequirePrivilege(...)</c> — unlike <c>AdminAccountEndpoints</c>, nothing here is data-dependent
/// on "is this the caller's own record," so there is no <c>RequireAuthenticatedCaller()</c> route.
/// </para>
/// <para>
/// EVERY MUTATING ENDPOINT CALLS <c>.RequireCsrfToken()</c> (CLAUDE.md §5) and declares
/// <c>Idempotency-Key</c> per the approved delta's table: REQUIRED on <c>POST</c> (a genuine create),
/// ACCEPTED on <c>PATCH</c>/<c>DELETE</c>.
/// </para>
/// <para>
/// Spec 6.1.7 rule 2 is enforced by <see cref="RolePrivilegeEscalationGuard"/> inside the create and
/// update handlers, not here — the route-level privilege gate only proves the caller holds
/// <c>role.create</c>/<c>role.update</c>, which is a different question from "holds every privilege
/// it is about to add to the role."
/// </para>
/// </remarks>
public sealed class RoleEndpoints : IEndpointModule
{
    private const string Tag = "Roles";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/roles")
            .WithTags(Tag);

        MapCreate(group);
        MapList(group);
        MapGet(group);
        MapUpdate(group);
        MapDelete(group);
    }

    private static void MapCreate(RouteGroupBuilder group) =>
        group.MapPost(string.Empty, async (
                CreateRoleCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);

                return result.Match(response => TypedResults.Created($"/api/v1/roles/{response.Id}", response));
            })
            .RequirePrivilege(Privileges.Role.Create)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CreateRole")
            .WithSummary("Create a role")
            .WithDescription(
                "Spec 6.1.4: name, description and at least one privilege code. Rejects the reserved " +
                "name `Super Admin`, case-insensitive, and any privilege code that does not resolve " +
                "(after legacy `guardian.*` alias resolution) to the register, naming the offender. " +
                "Spec 6.1.7 rule 2: every requested privilege must already be held by the caller — a " +
                "new role starts with none, so every one requested counts as an addition. " +
                "`Idempotency-Key` is REQUIRED: a retry with the same key returns the same role " +
                "instead of creating a second one.")
            .Produces<RoleDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapList(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] string? cursor = null,
                [FromQuery] int? pageSize = null,
                [FromQuery] RoleStatus? status = null,
                [FromQuery] string? search = null,
                [FromQuery] string? sort = null,
                [FromQuery] string? direction = null) =>
            {
                var result = await sender.SendAsync(
                    new ListRolesQuery(cursor, pageSize, status, search, sort, direction),
                    cancellationToken);

                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Role.View)
            .WithName("ListRoles")
            .WithSummary("List roles")
            .WithDescription(
                "Cursor-paginated per spec 9.5 — never offset. Archived roles are excluded unless " +
                "`status` names them explicitly (spec 9.4's default-scope rule). `search` matches the " +
                "role name, case-insensitively, by substring. `sort` is `name` (default) or `status`; " +
                "`direction` is `asc` (default) or `desc`. " +
                $"`pageSize` defaults to {CursorPageRequest.DefaultPageSize} and is capped at " +
                $"{CursorPageRequest.MaxPageSize}.")
            .Produces<CursorPage<RoleDto>>(StatusCodes.Status200OK)
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
                var result = await sender.SendAsync(new GetRoleQuery(id), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Role.View)
            .WithName("GetRole")
            .WithSummary("Read one role")
            .WithDescription(
                "Returns the role's own fields — assignments and effective privileges are TASK-0030's " +
                "detail-view additions, not this endpoint.")
            .Produces<RoleDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdate(RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateRoleCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { Id = id }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Role.Update)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("UpdateRole")
            .WithSummary("Edit a role")
            .WithDescription(
                "Every field is independently optional; an absent field is left unchanged (spec 6.1.4, " +
                "6.1.9). A system role (the seeded Super Admin) rejects the whole request with 409, " +
                "regardless of which fields it touches. A `privileges` array REPLACES the whole set; " +
                "spec 6.1.7 rule 2 applies only to codes newly present that were not already on the " +
                "role — removal is unrestricted. `Idempotency-Key` is accepted, not required.")
            .Produces<RoleDto>(StatusCodes.Status200OK)
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
                var result = await sender.SendAsync(new DeleteRoleCommand(id), cancellationToken);
                return result.Match(TypedResults.NoContent);
            })
            .RequirePrivilege(Privileges.Role.Delete)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("DeleteRole")
            .WithSummary("Delete a role")
            .WithDescription(
                "Spec 9.4: hard-deletes unconditionally today, because no `role_assignment` table " +
                "exists yet, so nothing can ever have referenced a role (TASK-0030 adds the " +
                "has-ever-been-assigned branch that archives instead). A system role returns 409.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
