using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// One arm's attendance sheet for one term (TASK-0086 stage A; spec §6.7.7, §6.7.12 amendment —
/// entered for a nursery arm too, even though it is not printed there). Maps routes under the same
/// <c>/arms</c> prefix <see cref="TraitRatingEndpoints"/> owns — a domain split, not a URL-prefix
/// one.
/// </summary>
/// <remarks>
/// ROUTE SHAPE IS ARM-SCOPED, NOT RESULT-SET-SCOPED — same departure from spec §6.7.13's literal
/// <c>PUT /result-sets/{id}/attendance</c>, for the same reason TASK-0083 departed for trait
/// ratings: the first save must work before a result set exists.
/// </remarks>
public sealed class AttendanceEndpoints : IEndpointModule
{
    private const string Tag = "Attendance";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/arms").WithTags(Tag);

        MapGetAttendance(group);
        MapSaveAttendance(group);
    }

    private static void MapGetAttendance(RouteGroupBuilder group) =>
        group.MapGet("/{armId:guid}/attendance", async (
                Guid armId,
                [FromQuery(Name = "termId")] string termId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetAttendanceQuery(armId.ToString(), termId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.View, ScopeParameterKind.Arm, "armId")
            .WithName("GetAttendance")
            .WithSummary("Read one arm's attendance sheet for a term")
            .WithDescription(
                "Spec 6.7.7/6.7.12 amendment: every active pupil in the arm as a row. `timesAbsent` " +
                "is derived from the term's `timesSchoolOpened` minus `timesPresent` (ruling A) and " +
                "is null while either side is unknown. `version` is null before any entry exists; " +
                "send it back unchanged on `PUT` to detect a concurrent edit.")
            .Produces<AttendanceSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSaveAttendance(RouteGroupBuilder group) =>
        group.MapPut("/{armId:guid}/attendance", async (
                Guid armId,
                SaveAttendanceCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { ArmId = armId.ToString() }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.AttendanceEnter, ScopeParameterKind.Arm, "armId")
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("SaveAttendance")
            .WithSummary("Save attendance, partial or whole sheet")
            .WithDescription(
                "Spec 6.7.7/6.7.12 amendment: partial-save, one transaction — a pupil omitted from " +
                "`rows` is left untouched, an explicit null `timesPresent` clears it. The first entry " +
                "for an arm and term creates its result set (Draft), never flags `needsRecompute` on " +
                "an EXISTING one. Allowed only while the result set is Draft or Returned for " +
                "Correction, else 409 `attendance.result_set_locked`. 422 `times_present_out_of_range` " +
                "below 0, or above the term's `timesSchoolOpened` when it is set (200 when blank).")
            .Produces<AttendanceSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
