using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Sessions;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Term lifecycle (TASK-0035; spec 6.3.2, 6.3.6, 6.3.9, 6.3.10). Session creation/editing
/// (<c>/sessions/*</c>) is <see cref="SessionEndpoints"/> — a separate route group, sharing this
/// module's domain and application slice.
/// </summary>
/// <remarks>
/// The one-active-term invariant (spec 6.3.9) is enforced by a database partial unique index
/// (<c>TermConfiguration</c>), not by anything below — <c>open</c>/<c>reopen</c> only produce a
/// friendly, named-reason 409 for the ordinary path; a data error would still hit the constraint.
/// </remarks>
public sealed class TermEndpoints : IEndpointModule
{
    private const string Tag = "Terms";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/terms")
            .WithTags(Tag);

        MapUpdate(group);
        MapOpen(group);
        MapClose(group);
        MapReopen(group);
    }

    private static void MapUpdate(RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateTermCommand command,
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
            .WithName("UpdateTerm")
            .WithSummary("Edit a term's schedule")
            .WithDescription(
                "Spec 6.3.10: dates, label, times school opened, next resumption date. Every field " +
                "is independently optional; an absent field is left unchanged. Times school opened " +
                "is rejected once the term is `closed` (spec 6.3.6: printed on results already " +
                "issued), and refused with 409 `term.times_school_opened_below_attendance` if it " +
                "would be set below the highest `timesPresent` already recorded for this term on any " +
                "arm's attendance sheet (TASK-0086 delta item 5) — the derived times-absent would " +
                "otherwise go negative. `Idempotency-Key` is accepted, not required.")
            .Produces<TermDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapOpen(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/open", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new OpenTermCommand(id), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Term.Open)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("OpenTerm")
            .WithSummary("Open a term")
            .WithDescription(
                "Spec 6.3.6: moves `upcoming` to `active`. Blocked unless the previous term in the " +
                "session is closed (or this is ordinal 1 and no term anywhere is currently active). " +
                "The rejection names the reason. Opening ordinal 1 also moves this session to " +
                "`active` and the previously active session (if any) to `closed` (spec 6.3.5). The " +
                "\"at least one arm exists\" precondition (spec 6.3.6) is NOT checked — Arm does not " +
                "exist in this codebase yet, so `open` is more permissive than spec until that card " +
                "lands.")
            .Produces<TermDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapClose(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/close", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new CloseTermCommand(id), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Term.Close)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CloseTerm")
            .WithSummary("Close a term")
            .WithDescription(
                "Spec 6.3.6, 6.3.9: moves `active` to `closed`, writing `closedAtUtc`/`closedBy`. " +
                "Rejected if `timesSchoolOpened` is blank, naming the term. The result-set precondition " +
                "(spec 6.3.6: blocked by Draft/Awaiting Approval/Approved sets) is NOT checked — " +
                "result sets do not exist in this codebase yet, so `close` is more permissive than " +
                "spec until that card lands.")
            .Produces<TermDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapReopen(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/reopen", async (
                Guid id,
                ReopenTermCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { Id = id }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Term.Close)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("ReopenTerm")
            .WithSummary("Reopen a closed term")
            .WithDescription(
                "Spec 6.3.6: `term.close` PLUS `isSuperAdmin` (checked in the handler — not a " +
                "privilege code), a reason of at least 10 characters, and refused outright if the " +
                "following term has already been opened.")
            .Produces<TermDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
