using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Subjects, level mappings and per-arm exceptions (TASK-0070; spec 6.6). Eight paths: subject CRUD,
/// the whole-term mapping grid (read, save, copy, prefill), one arm's resolved subject set, and
/// per-arm exception management.
/// </summary>
/// <remarks>
/// <c>GET /arms/{id}/subjects</c> and <c>POST /arms/{id}/subject-exceptions</c> map ROUTES under the
/// same <c>/arms</c> prefix <see cref="ArmEndpoints"/> owns — a deliberate split by DOMAIN (this
/// card's own subject-mapping resolution and exceptions), not by URL prefix; <c>IEndpointModule</c>
/// registration allows more than one module to add routes under one group.
/// </remarks>
public sealed class SubjectEndpoints : IEndpointModule
{
    private const string SubjectsTag = "Subjects";
    private const string SubjectMappingsTag = "Subject mappings";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var subjects = endpoints.MapGroup("/subjects").WithTags(SubjectsTag);
        MapListSubjects(subjects);
        MapCreateSubject(subjects);
        MapUpdateSubject(subjects);
        MapDeleteSubject(subjects);

        var mappings = endpoints.MapGroup("/subject-mappings").WithTags(SubjectMappingsTag);
        MapGetGrid(mappings);
        MapSaveGrid(mappings);
        MapCopyGrid(mappings);
        MapPrefillGrid(mappings);

        var exceptions = endpoints.MapGroup("/subject-exceptions").WithTags(SubjectMappingsTag);
        MapDeleteException(exceptions);

        var arms = endpoints.MapGroup("/arms").WithTags(SubjectMappingsTag);
        MapGetArmSubjects(arms);
        MapCreateException(arms);
    }

    private static void MapListSubjects(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] string? cursor = null,
                [FromQuery] int? pageSize = null,
                [FromQuery] SubjectStatus? status = null,
                [FromQuery] string? levelId = null,
                [FromQuery] string? termId = null) =>
            {
                var query = new ListSubjectsQuery(cursor, pageSize, status, levelId, termId);
                var result = await sender.SendAsync(query, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Subject.View)
            .WithName("ListSubjects")
            .WithSummary("List subjects")
            .WithDescription(
                "Spec 6.6.7: name, code, status, and — only when `termId` is supplied — the number " +
                "of levels mapped, arm exceptions and pupils currently taking it this term. This " +
                "endpoint never guesses a term: with `termId` absent, those three counts are null " +
                "rather than resolved against a guessed term. Default sort by name ascending, " +
                "cursor-paginated per spec 9.5.")
            .Produces<CursorPage<SubjectDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapCreateSubject(RouteGroupBuilder group) =>
        group.MapPost(string.Empty, async (
                CreateSubjectCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(response => TypedResults.Created($"/api/v1/subjects/{response.Id}", response));
            })
            .RequirePrivilege(Privileges.Subject.Create)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CreateSubject")
            .WithSummary("Create a subject")
            .WithDescription(
                "Spec 6.6.2. `code` is OPTIONAL (TASK-0070 delta amendment 1) — nullable, unseeded, " +
                "uppercase letters and digits when supplied. `Idempotency-Key` is REQUIRED.")
            .Produces<SubjectDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdateSubject(RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateSubjectCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { Id = id }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Subject.Update)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("UpdateSubject")
            .WithSummary("Edit a subject")
            .WithDescription(
                "Spec 6.6.2. Every field is independently optional; an absent field is left " +
                "unchanged; an empty string clears `code`/`description`. Changing `status` " +
                "ADDITIONALLY requires `subject.deactivate`, beyond the `subject.update` this route " +
                "requires.")
            .Produces<SubjectDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapDeleteSubject(RouteGroupBuilder group) =>
        group.MapDelete("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new DeleteSubjectCommand(id), cancellationToken);
                return result.Match(TypedResults.NoContent);
            })
            .RequirePrivilege(Privileges.Subject.Delete)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("DeleteSubject")
            .WithSummary("Delete a subject")
            .WithDescription("Spec 6.6.8: permitted only where it has never been mapped and never scored. Otherwise deactivate.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapGetGrid(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                [FromQuery(Name = "term_id")] string termId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetSubjectMappingGridQuery(termId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Subject.View)
            .WithName("GetSubjectMappingGrid")
            .WithSummary("Read the subject mapping grid for a term")
            .WithDescription("Spec 6.6.5, 6.6.9: levels, subjects, ticks, and the per-arm exception summary.")
            .Produces<SubjectMappingGridDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSaveGrid(RouteGroupBuilder group) =>
        group.MapPut(string.Empty, async (
                [FromQuery(Name = "term_id")] string termId,
                SaveSubjectMappingGridCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { TermId = termId }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Subject.View)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("SaveSubjectMappingGrid")
            .WithSummary("Save the whole subject mapping grid for a term")
            .WithDescription(
                "Spec 6.6.5, 6.6.9: whole-grid save, atomic. `dryRun` returns the additions/endings " +
                "preview without writing. TASK-0070 delta amendment 2: requires `subject.map` when " +
                "the computed diff has additions and `subject.unmap` when it has endings — both when " +
                "it has both — evaluated identically under `dryRun`. Rejected outright against a " +
                "closed term; an ending is rejected when marks exist for that subject in any arm " +
                "under the level (spec 6.6.6).")
            .Produces<SaveSubjectMappingGridResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapCopyGrid(RouteGroupBuilder group) =>
        group.MapPost("/copy", async (
                CopySubjectMappingsCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Subject.Map)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CopySubjectMappings")
            .WithSummary("Copy a term's subject mappings into another term")
            .WithDescription(
                "Spec 6.6.5, 6.6.9. Additive only — never ends a destination mapping the source " +
                "does not have, and never duplicates one already active in the destination. " +
                "Requires `subject.map` only. `Idempotency-Key` is REQUIRED.")
            .Produces<SaveSubjectMappingGridResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapPrefillGrid(RouteGroupBuilder group) =>
        group.MapPost("/prefill", async (
                PrefillSubjectMappingsCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Subject.Map)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("PrefillSubjectMappings")
            .WithSummary("Apply the school's standard subject list to a term")
            .WithDescription(
                "TASK-0070 delta amendment 5 (human-ruled 2026-09-16; not in spec 6.6.9's own list) — " +
                "the 14/19 standard nursery/primary list, `display_order` from the school's own " +
                "result-sheet order. Additive only: can never end a mapping and never duplicates an " +
                "existing active one. Requires `subject.map` only. `Idempotency-Key` is REQUIRED. " +
                "Rejected outright against a closed term, `dryRun` included.")
            .Produces<SaveSubjectMappingGridResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapGetArmSubjects(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}/subjects", async (
                Guid id,
                [FromQuery(Name = "term_id")] string termId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetArmSubjectsQuery(id.ToString(), termId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Subject.View)
            .WithName("GetArmSubjects")
            .WithSummary("Read one arm's resolved subject set")
            .WithDescription(
                "Spec 6.6.1, 6.6.4, 6.6.9: level mappings for the term, plus the arm's include " +
                "exceptions, minus its exclude exceptions — each row flagged level-inherited or arm " +
                "exception. The endpoint the score entry screen and the result renderer both call. " +
                "Unwrapped array, no pagination.")
            .Produces<IReadOnlyList<ArmSubjectDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapCreateException(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/subject-exceptions", async (
                Guid id,
                CreateSubjectExceptionCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { ArmId = id.ToString() }, cancellationToken);
                return result.Match(response => TypedResults.Created($"/api/v1/subject-exceptions/{response.Id}", response));
            })
            .RequirePrivilege(Privileges.Subject.MapArm)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CreateSubjectException")
            .WithSummary("Create a per-arm subject exception")
            .WithDescription(
                "Spec 6.6.4, 6.6.9. `include` adds a subject the level does not take; `exclude` " +
                "removes one the level does take. Rejected as redundant when the mode already " +
                "matches the level's own mapping, and when the arm's session is closed. " +
                "`Idempotency-Key` is REQUIRED.")
            .Produces<SubjectExceptionDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapDeleteException(RouteGroupBuilder group) =>
        group.MapDelete("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new DeleteSubjectExceptionCommand(id), cancellationToken);
                return result.Match(TypedResults.NoContent);
            })
            .RequirePrivilege(Privileges.Subject.MapArm)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("DeleteSubjectException")
            .WithSummary("Delete a per-arm subject exception")
            .WithDescription(
                "Spec 6.6.4, 6.6.9. TASK-0070 delta amendment 2: requires `subject.map.arm`, the " +
                "same privilege as its create — NOT `subject.unmap`. No closed-session 409: spec " +
                "6.6.8's closed-session rejection applies to creation only.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
