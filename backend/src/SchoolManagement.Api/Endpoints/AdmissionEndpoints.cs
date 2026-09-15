using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Admissions;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// The admissions queue read surface (TASK-0050; spec 6.5.14, 6.5.17) and section A/I/J's write
/// surface (TASK-0062). Steps 2 to 8 and the nine-step flow's resumability/approval are later cards.
/// </summary>
public sealed class AdmissionEndpoints : IEndpointModule
{
    private const string Tag = "Admissions";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/admissions")
            .WithTags(Tag);

        MapListQueue(group);
        MapUpdate(group);
    }

    private static void MapListQueue(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] string? cursor = null,
                [FromQuery] int? pageSize = null) =>
            {
                var result = await sender.SendAsync(new ListAdmissionsQueueQuery(cursor, pageSize), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Pupil.View, ScopeParameterKind.None)
            .WithName("ListAdmissionsQueue")
            .WithSummary("List the admissions queue")
            .WithDescription(
                "Spec 6.5.14: \"A pending pupil is excluded from every arm roster... It exists in the " +
                "admissions queue and nowhere else.\" This IS that queue — every pending pupil, " +
                "unconditionally. Each row additionally carries `levelAppliedFor`, " +
                "`dateApplicationReceived` and `missing` (TASK-0062; spec 6.5.15) — `missing` covers " +
                "only what sections A and I's stored fields can check today (the assessment result and " +
                "the declaration): steps 2 to 8 have no entity yet, so a gap there never appears. " +
                "SCHOOL-WIDE `pupil.view` only: a pending record has no arm yet, so an arm-scoped grant " +
                "has no meaningful reach here (unlike `GET /pupils`, which still admits an arm-scoped " +
                "caller to an honest empty page). Cursor-paginated per spec 9.5.")
            .Produces<CursorPage<PupilDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapUpdate(RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateAdmissionRecordCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { Id = id }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Pupil.Update, ScopeParameterKind.None)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("UpdateAdmissionRecord")
            .WithSummary("Edit an admission record's sections A, I and J")
            .WithDescription(
                "Spec 6.5.9, 6.5.11: `id` is the PUPIL id, the same one `POST /pupils` returned and " +
                "the admissions queue lists rows by. Steps 2 to 8's own screens are a later card — this " +
                "endpoint only ever touches sections A, I and J. A HALF-FINISHED STEP STILL SAVES: " +
                "every field is independently optional, absent fields are left unchanged, and an empty " +
                "string clears an optional string field. `assessmentResultRemarks` is NOT required to " +
                "save even when `assessmentRequired` is true — only admission APPROVAL (a later card) " +
                "enforces that. 404 when `id` does not name a PENDING pupil (approved, declined or " +
                "unknown pupils are not reachable through this route). Issues no registration number " +
                "and approves nothing — `pupil.update`, SCHOOL-WIDE only (see `ListAdmissionsQueue`'s " +
                "own description for why an arm-scoped grant cannot reach a pending record).")
            .Produces<AdmissionRecordDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
