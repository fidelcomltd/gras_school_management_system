using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// The pupil register's entity and read surface (TASK-0050; spec 6.5.4, 6.5.10, 6.5.14, 6.5.15).
/// Contacts, health, pickup/barred persons, documents, the admission flow, photograph upload, bulk
/// import, transfer and every status transition are later cards — see the task card's own
/// out-of-scope list. <c>GET /admissions</c> is <see cref="AdmissionEndpoints"/>, a separate module.
/// </summary>
public sealed class PupilEndpoints : IEndpointModule
{
    private const string Tag = "Pupils";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/pupils")
            .WithTags(Tag);

        MapCreate(group);
        MapList(group);
        MapFindDuplicates(group);
        MapGet(group);
        MapUpdate(group);
    }

    private static void MapCreate(RouteGroupBuilder group) =>
        group.MapPost(string.Empty, async (
                CreatePupilCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(response => TypedResults.Created($"/api/v1/pupils/{response.Id}", response));
            })
            .RequirePrivilege(Privileges.Pupil.Create)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CreatePupil")
            .WithSummary("Register a new pupil")
            .WithDescription(
                "Spec 6.5.4: always creates a PENDING record with no registration number — nothing " +
                "reaches active until admission approval (a later card) issues one. `pupil.create` is " +
                "not arm-scoped. `Idempotency-Key` is REQUIRED.")
            .Produces<PupilDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapList(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] string? cursor = null,
                [FromQuery] int? pageSize = null,
                [FromQuery] PupilStatus? status = null,
                [FromQuery] string? search = null) =>
            {
                var result = await sender.SendAsync(new ListPupilsQuery(cursor, pageSize, status, search), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequireAuthenticatedCaller()
            .WithName("ListPupils")
            .WithSummary("List pupils")
            .WithDescription(
                "Spec 6.5.15. Cursor-paginated per spec 9.5, page size 50 by default. Sorted by " +
                "surname ascending then id (spec 6.5.15's own default additionally orders by class " +
                "progression, which no pupil in this card can carry — see " +
                "backend/docs/ASSUMPTIONS.md §2.27). EXCLUDES pending unless `status=Pending` is " +
                "passed explicitly (the pending-exclusion invariant, spec 6.5.14) — `GET /admissions` " +
                "is the queue that always shows it. `search` matches surname, first name, middle " +
                "name or the registration number (in full, or its trailing serial — spec 6.5.15's " +
                "own worked example, \"41\" finding \"GRAS/2026/0041\", is satisfied by ordinary " +
                "substring matching); contact-phone and authorised-pickup-person search are the NEXT " +
                "card's, once `pupil_contact` exists. `pupil.view` is arm-scoped for a Class Teacher: " +
                "no pupil carries an arm reference until enrolment exists, so an arm-scoped caller " +
                "sees an empty page today rather than a 403 — a real answer, not a placeholder.")
            .Produces<CursorPage<PupilDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapFindDuplicates(RouteGroupBuilder group) =>
        group.MapGet("/duplicates", async (
                [FromQuery] string surname,
                [FromQuery] string firstName,
                [FromQuery] DateOnly dateOfBirth,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(
                    new FindPupilDuplicatesQuery(surname, firstName, dateOfBirth), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Pupil.Create)
            .WithName("FindPupilDuplicates")
            .WithSummary("Find pupil records that may be duplicates")
            .WithDescription(
                "Spec 6.5.11 step 1: matches surname AND first name AND date of birth, INCLUDING " +
                "pending records — the point is catching a second, in-progress admission for the same " +
                "child. Contact-phone matching (the other half of 6.5.11's detection) is the NEXT " +
                "card's, once `pupil_contact` exists. Not arm-scoped: `pupil.create` is school-wide " +
                "only. Capped at 20 candidates — a panel, not a paged list.")
            .Produces<IReadOnlyList<PupilDto>>(StatusCodes.Status200OK)
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
                var result = await sender.SendAsync(new GetPupilQuery(id), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequireAuthenticatedCaller()
            .WithName("GetPupil")
            .WithSummary("Read one pupil")
            .WithDescription(
                "Renders only what exists after this card — no contacts, health, documents or " +
                "enrolment-history blocks yet. Reachable for a PENDING record (finding it again to " +
                "edit it is this card's own goal), unlike the list. `pupil.view` is arm-scoped for a " +
                "Class Teacher; see `ListPupils`'s description for why an arm-scoped caller cannot " +
                "reach any pupil yet.")
            .Produces<PupilDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdate(RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdatePupilBiographicalCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { Id = id }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequireAuthenticatedCaller()
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("UpdatePupilBiographical")
            .WithSummary("Edit a pupil's biographical fields")
            .WithDescription(
                "Spec 6.5.10: `registrationNumber` in the payload is REJECTED with 409 — it is " +
                "issued once at admission approval and never edited afterwards, \"no ordinary edit " +
                "path exists.\" Every other field is independently optional; an absent field is left " +
                "unchanged, an empty string clears an optional one. `pupil.update` is arm-scoped for " +
                "a Class Teacher; see `ListPupils`'s description for why an arm-scoped caller cannot " +
                "reach any pupil yet.")
            .Produces<PupilDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
