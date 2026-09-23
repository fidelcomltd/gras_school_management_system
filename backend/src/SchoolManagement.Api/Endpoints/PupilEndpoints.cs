using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Application.Pupils.Import;
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
        MapCorrectRegistrationNumber(group);
        MapImportTemplate(group);
        MapImportValidate(group);
        MapImportCommit(group);
    }

    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Bytes over the processor's own cap that a multipart envelope's boundaries and fields may add.</summary>
    private const long MultipartOverheadBytes = 64 * 1024;

    private static void MapImportTemplate(RouteGroupBuilder group) =>
        group.MapGet("/import/template", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetPupilImportTemplateQuery(), cancellationToken);
                return result.Match(file => TypedResults.File(file.Content.ToArray(), XlsxContentType, file.FileName));
            })
            .RequirePrivilege(Privileges.Pupil.Import)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("GetPupilImportTemplate")
            .WithSummary("Download the bulk-import template")
            .WithDescription(
                "Spec 6.5.13: an XLSX workbook. Sheet `Pupils` holds the header row, one column per spec field; sheet " +
                "`Accepted values` lists sex, state of origin, suggested relationships, blood group, genotype, admission type, " +
                "yes/no, primary contact, and the active session's open arms (class level, arm label, composed name); sheet " +
                "`LGAs` lists every state's LGAs. With no active session the arm columns are empty.")
            .Produces<Stream>(StatusCodes.Status200OK, XlsxContentType)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapImportValidate(RouteGroupBuilder group) =>
        group.MapPost("/import/validate", async (
                IFormFile file,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var bytes = await file.ReadAllBytesAsync(cancellationToken).ConfigureAwait(false);
                var result = await sender.SendAsync(new ValidatePupilImportQuery(bytes), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Pupil.Import)
            .RequireCsrfToken()
            // Our own CSRF filter protects this route; ASP.NET's automatic IFormFile antiforgery check would otherwise 500.
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithMetadata(new RequestSizeLimitAttribute(PupilImportLimits.MaxFileBytes + MultipartOverheadBytes))
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("ValidatePupilImport")
            .WithSummary("Validate a bulk-import file")
            .WithDescription(
                "Spec 6.5.13. Multipart, one `file` part (XLSX, at most 5 MB and 1000 pupils). WRITES NOTHING. Returns every " +
                "row accepted or rejected, each rejection naming its column and reason; accepted rows matching a pupil already " +
                "on the register (same surname, first name and date of birth) carry `registerMatches` and need a skip or " +
                "create decision at commit; `capacityWarnings` lists arms the file would take over capacity. Rows in the " +
                "file duplicating an earlier row are rejected. Whole-file problems are 422: `import.file_invalid`, " +
                "`import.file_empty`, `import.too_many_rows`, `import.missing_columns`; 409 `import.no_active_session`.")
            .Produces<PupilImportReportDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapImportCommit(RouteGroupBuilder group) =>
        group.MapPost("/import/commit", async (
                IFormFile file,
                [FromForm] string fileSha256,
                [FromForm] int[]? skipRows,
                [FromForm] int[]? createRows,
                [FromForm] bool? overrideCapacity,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var bytes = await file.ReadAllBytesAsync(cancellationToken).ConfigureAwait(false);
                var command = new CommitPupilImportCommand(bytes, fileSha256, skipRows ?? [], createRows ?? [], overrideCapacity ?? false);
                var result = await sender.SendAsync(command, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Pupil.Import)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(PupilImportLimits.MaxFileBytes + MultipartOverheadBytes))
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CommitPupilImport")
            .WithSummary("Import a validated file")
            .WithDescription(
                "Spec 6.5.13. Multipart: the SAME `file` again, `fileSha256` from its report, `skipRows` and `createRows` " +
                "(repeated fields, sheet row numbers) deciding every register match, and `overrideCapacity=true` to import " +
                "past an arm's capacity (needs `arm.capacity.override`). The file is re-validated and imported ALL OR NOTHING: " +
                "each pupil is created active, with a registration number issued in file order by the same counter as " +
                "admission approval, an open enrolment, and an admission record with the declaration unsigned; health " +
                "columns left blank stay unanswered. 409 `import.file_changed`, `import.capacity_unconfirmed`; 422 " +
                "`import.rows_rejected`, `import.decision_missing`, `import.decision_conflict`, `import.decision_unexpected`, " +
                "`import.nothing_to_import`; 403 `import.capacity_override_forbidden`. `Idempotency-Key` is REQUIRED.")
            .Produces<PupilImportResultDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

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

    private static void MapCorrectRegistrationNumber(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/registration-number", async (
                Guid id,
                CorrectRegistrationNumberCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { Id = id }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Pupil.RegNumberCorrect)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: true)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("CorrectPupilRegistrationNumber")
            .WithSummary("Correct a wrongly issued registration number")
            .WithDescription(
                "Spec 6.5.10, \"Immutability and correction\". Super Admin only " +
                "(`pupil.regnumber.correct` is excluded from every other seeded role). The " +
                "administrator TYPES the replacement number in full — nothing here composes one " +
                "from settings, unlike admission approval's issuance. The new number must be " +
                "unique against both the live `registrationNumber` column and every historical " +
                "alias ever recorded (409 on either collision). The counter is NOT touched — a " +
                "correction consumes no serial. The old number is written to a permanent history " +
                "row with the reason, the actor and the timestamp, and is never deleted: a parent " +
                "holding a pin slip printed with the old number still reaches this pupil (the " +
                "portal lookup that reads it is a later card). Already-published result snapshots " +
                "are not rewritten. `Idempotency-Key` is REQUIRED — a retry must not append a " +
                "second, redundant history row. 404 when `id` names no pupil. 409 when this pupil " +
                "has no registration number yet (still pending — approve the admission first).")
            .Produces<PupilDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
