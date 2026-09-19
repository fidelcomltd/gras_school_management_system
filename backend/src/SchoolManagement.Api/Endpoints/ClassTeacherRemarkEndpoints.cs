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
/// One arm's class-teacher remark sheet for one term (TASK-0086 stage A; spec §6.7.7; appendix
/// C.6). Maps routes under the same <c>/arms</c> prefix <see cref="TraitRatingEndpoints"/> owns.
/// </summary>
public sealed class ClassTeacherRemarkEndpoints : IEndpointModule
{
    private const string Tag = "Remarks";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/arms").WithTags(Tag);

        MapGetClassTeacherRemarks(group);
        MapSaveClassTeacherRemarks(group);
    }

    private static void MapGetClassTeacherRemarks(RouteGroupBuilder group) =>
        group.MapGet("/{armId:guid}/class-teacher-remarks", async (
                Guid armId,
                [FromQuery(Name = "termId")] string termId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetClassTeacherRemarksQuery(armId.ToString(), termId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.View, ScopeParameterKind.Arm, "armId")
            .WithName("GetClassTeacherRemarks")
            .WithSummary("Read one arm's class-teacher remark sheet for a term")
            .WithDescription(
                "Spec 6.7.7, appendix C.6: every active pupil in the arm as a row. `version` is null " +
                "before any remark exists; send it back unchanged on `PUT` to detect a concurrent edit.")
            .Produces<RemarkSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSaveClassTeacherRemarks(RouteGroupBuilder group) =>
        group.MapPut("/{armId:guid}/class-teacher-remarks", async (
                Guid armId,
                SaveClassTeacherRemarksCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command with { ArmId = armId.ToString() }, cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.RemarkClassTeacher, ScopeParameterKind.Arm, "armId")
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("SaveClassTeacherRemarks")
            .WithSummary("Save class-teacher remarks, partial or whole sheet")
            .WithDescription(
                "Spec 6.7.7, appendix C.6: partial-save, one transaction — a pupil omitted from " +
                "`rows` is left untouched; an empty or whitespace-only `remark` clears it, like null. " +
                "Text is trimmed. The first remark of either kind for an arm and term creates its " +
                "result set (Draft). Allowed only while the result set is Draft or Returned for " +
                "Correction (§6.7.11: submission locks it), else 409 " +
                "`class_teacher_remarks.result_set_locked`. 422 `remark_too_long` above 300 characters " +
                "(ruling L).")
            .Produces<RemarkSheetDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
