using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Security.Assignments;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Role assignment grant/revoke (TASK-0030; spec 6.1.5, 6.1.7 rules 1 and 3, 6.1.14). Rule 2,
/// copy-to-session, the read-surface fields and the 6.1.13 lifecycle cascades are TASK-0046.
/// </summary>
/// <remarks>
/// <para>
/// <c>POST /admins/{id}/assignments</c> IS MAPPED WITH <c>RequireAuthenticatedCaller()</c> RATHER
/// THAN <c>RequirePrivilege(...)</c>: the required privilege is data-dependent on the request body's
/// <c>scopeType</c> (<c>role.assign</c> for school-wide, <c>role.scope.assign</c> for arm-list) —
/// same reasoning as <c>AdminAccountEndpoints</c>'s <c>PATCH</c>/<c>status</c> routes. The GET and
/// DELETE routes each have one fixed privilege and are gated declaratively.
/// </para>
/// <para>
/// EVERY MUTATING ENDPOINT CALLS <c>.RequireCsrfToken()</c> (CLAUDE.md §5). <c>Idempotency-Key</c> is
/// REQUIRED on <c>POST</c> (a genuine create) and ACCEPTED on <c>DELETE</c> (revoking an
/// already-revoked assignment converges, per the approved delta's table).
/// </para>
/// </remarks>
public sealed class AssignmentEndpoints : IEndpointModule
{
    private const string Tag = "Assignments";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var adminAssignments = endpoints
            .MapGroup("/admins/{id:guid}/assignments")
            .WithTags(Tag);

        MapList(adminAssignments);
        MapCreate(adminAssignments);

        var assignments = endpoints
            .MapGroup("/assignments")
            .WithTags(Tag);

        MapRevoke(assignments);
    }

    private static void MapList(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(
                    new ListRoleAssignmentsQuery(id.ToString()), cancellationToken);

                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Admin.View)
            .WithName("ListRoleAssignments")
            .WithSummary("List an account's role assignments")
            .WithDescription(
                "Spec 6.1.5: every assignment for this account, active and revoked, with role, scope " +
                "type, scope ids and session. Not paginated — assignment counts per account are " +
                "admin-configuration-sized.")
            .Produces<IReadOnlyList<RoleAssignmentDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapCreate(RouteGroupBuilder group) =>
        group.MapPost(string.Empty, async (
                Guid id,
                CreateRoleAssignmentCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(
                    command with { AdminAccountId = id.ToString() }, cancellationToken);

                return result.Match(response => TypedResults.Created($"/api/v1/assignments/{response.Id}", response));
            })
            .RequireAuthenticatedCaller()
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CreateRoleAssignment")
            .WithSummary("Grant a role to an account")
            .WithDescription(
                "Spec 6.1.5: role, session and either school-wide or a non-empty arm list; every arm " +
                "must belong to the named session. Requires `role.assign` for a school-wide grant, or " +
                "`role.scope.assign` for an arm-scoped one. Escalation rule 1 (spec 6.1.7): rejects an " +
                "attempt to assign a role to yourself — `You cannot change your own roles. Ask " +
                "another Super Admin.` Rule 3: rejects a grant wider than the caller's own scope for " +
                "the privilege it is exercising. Both rejections write an audit event. The seeded " +
                "Super Admin role cannot be assigned here — `is_super_admin` is a flag, set only " +
                "through `PATCH /admins/{id}`. `Idempotency-Key` is REQUIRED.")
            .Produces<RoleAssignmentDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapRevoke(RouteGroupBuilder group) =>
        group.MapDelete("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new RevokeRoleAssignmentCommand(id.ToString()), cancellationToken);
                return result.Match(TypedResults.NoContent);
            })
            .RequirePrivilege(Privileges.Role.Assign)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("RevokeRoleAssignment")
            .WithSummary("Revoke a role assignment")
            .WithDescription(
                "Spec 6.1.14. Requires `role.assign`. Escalation rule 1 (spec 6.1.7): rejects revoking " +
                "your own assignment, with an audit event on rejection. Always `204`.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
