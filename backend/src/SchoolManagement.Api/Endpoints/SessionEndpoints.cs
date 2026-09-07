using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Sessions;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Academic sessions (TASK-0035; spec 6.3.2, 6.3.5, 6.3.8, 6.3.9, 6.3.10). Term lifecycle
/// (<c>/terms/*</c>) is <see cref="TermEndpoints"/> — a separate route group, sharing this module's
/// domain and application slice.
/// </summary>
/// <remarks>
/// <c>POST /sessions</c> IS THE ONLY ROUTE THAT CREATES AN <c>academic_session</c> ROW, and it always
/// creates all three terms in the same transaction (spec 6.3.5: "There is no route that creates a
/// session without terms"). Every route here is gated by ONE FIXED privilege declaratively — nothing
/// is data-dependent the way two <c>/admins*</c> routes are.
/// </remarks>
public sealed class SessionEndpoints : IEndpointModule
{
    private const string Tag = "Sessions";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/sessions")
            .WithTags(Tag);

        MapCreate(group);
        MapList(group);
        MapGet(group);
        MapUpdate(group);
    }

    private static void MapCreate(RouteGroupBuilder group) =>
        group.MapPost(string.Empty, async (
                CreateSessionCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);

                return result.Match(response => TypedResults.Created($"/api/v1/sessions/{response.Id}", response));
            })
            .RequirePrivilege(Privileges.Session.Create)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CreateSession")
            .WithSummary("Create a session")
            .WithDescription(
                "Spec 6.3.5: creates the session AND its three terms in one transaction, all " +
                "`upcoming`. Name must be `YYYY/YYYY` with the second year exactly the first plus " +
                "one, and unique; dates must not overlap an existing session. `Idempotency-Key` is " +
                "REQUIRED: a retry with the same key returns the same session instead of creating a " +
                "second one.")
            .Produces<SessionDetailDto>(StatusCodes.Status201Created)
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
                [FromQuery] SessionState? state = null) =>
            {
                var result = await sender.SendAsync(new ListSessionsQuery(cursor, pageSize, state), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Session.View)
            .WithName("ListSessions")
            .WithSummary("List sessions")
            .WithDescription(
                "Cursor-paginated per spec 9.5, always sorted `name` descending — newest first (spec " +
                "6.3.8). `state` is the only filter. " +
                $"`pageSize` defaults to {CursorPageRequest.DefaultPageSize} and is capped at " +
                $"{CursorPageRequest.MaxPageSize}.")
            .Produces<CursorPage<SessionDto>>(StatusCodes.Status200OK)
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
                var result = await sender.SendAsync(new GetSessionQuery(id), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Session.View)
            .WithName("GetSession")
            .WithSummary("Read one session with its terms")
            .WithDescription(
                "Arms grouped by level, enrolment counts and the publication position (spec 6.3.8) " +
                "are not yet in this response — they need Arm/Pupil/result sets, which do not exist " +
                "in this codebase yet.")
            .Produces<SessionDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdate(RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateSessionCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { Id = id }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Session.Update)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("UpdateSession")
            .WithSummary("Edit a session's name and dates")
            .WithDescription(
                "Spec 6.3.10: name and dates, while `upcoming` or `active` — a `closed` session " +
                "returns 409. Every field is independently optional; an absent field is left " +
                "unchanged. `Idempotency-Key` is accepted, not required.")
            .Produces<SessionDetailDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
