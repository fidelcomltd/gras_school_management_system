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
/// One arm's head-teacher remark sheet for one term (TASK-0086 stage A; spec §6.7.7; appendix
/// C.6). Maps routes under the same <c>/arms</c> prefix <see cref="TraitRatingEndpoints"/> owns.
/// </summary>
/// <remarks>
/// <c>result.remark.headteacher</c> is SCHOOL-WIDE, not arm-scoped (spec 6.7.2) — the route still
/// names an arm in its path (to reach that arm's sheet) but declares
/// <see cref="ScopeParameterKind.None"/>, so any caller holding the privilege at all can save any
/// arm's head-teacher remark. This is the correct declaration for a non-scopable privilege, not an
/// oversight — <c>PrivilegeRequirement</c>'s constructor throws at boot if a scope parameter is
/// attached to a privilege the registry marks non-scopable.
/// </remarks>
public sealed class HeadTeacherRemarkEndpoints : IEndpointModule
{
    private const string Tag = "Remarks";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/arms").WithTags(Tag);

        MapGetHeadTeacherRemarks(group);
        MapSaveHeadTeacherRemarks(group);
    }

    private static void MapGetHeadTeacherRemarks(RouteGroupBuilder group) =>
        group.MapGet("/{armId:guid}/head-teacher-remarks", async (
                Guid armId,
                [FromQuery(Name = "termId")] string termId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetHeadTeacherRemarksQuery(armId.ToString(), termId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.View, ScopeParameterKind.Arm, "armId")
            .WithName("GetHeadTeacherRemarks")
            .WithSummary("Read one arm's head-teacher remark sheet for a term")
            .WithDescription(
                "Spec 6.7.7, appendix C.6: every active pupil in the arm as a row. `version` is null " +
                "before any remark exists; send it back unchanged on `PUT` to detect a concurrent edit.")
            .Produces<RemarkSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSaveHeadTeacherRemarks(RouteGroupBuilder group) =>
        group.MapPut("/{armId:guid}/head-teacher-remarks", async (
                Guid armId,
                SaveHeadTeacherRemarksCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { ArmId = armId.ToString() }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.RemarkHeadTeacher)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("SaveHeadTeacherRemarks")
            .WithSummary("Save head-teacher remarks, partial or whole sheet, with an optional fill-all")
            .WithDescription(
                "Spec 6.7.7, appendix C.6: partial-save, one transaction — a pupil omitted from " +
                "`rows` is left untouched; an empty or whitespace-only `remark` clears it, like null. " +
                "`fillEmpty` (ruling H) sets that text for every pupil who STILL has no remark once " +
                "`rows` has applied, never overwriting an existing one. The first remark of either " +
                "kind for an arm and term creates its result set (Draft). Allowed in Draft, Returned " +
                "for Correction, Awaiting Approval or Approved; 409 " +
                "`head_teacher_remarks.result_set_locked` once Published or Withdrawn. 422 " +
                "`remark_too_long` above 300 characters (ruling L), applied to both `rows` and " +
                "`fillEmpty`.")
            .Produces<RemarkSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
