using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>Result-set-scoped operations (TASK-0071; spec 09 §6.7.6). First route under <c>/result-sets</c>.</summary>
/// <remarks>
/// Scoped by <see cref="ScopeParameterKind.ResultSet"/> — the route names the result set directly, and
/// <c>PrivilegeAuthorizationHandler</c> resolves its arm via <c>IResultSetArmLookup</c> (TASK-0076
/// dispatch A wired the lookup; this is the first route to actually declare it).
/// </remarks>
public sealed class ResultSetEndpoints : IEndpointModule
{
    private const string Tag = "Result sets";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/result-sets").WithTags(Tag);

        MapCompute(group);
        MapSubmit(group);
    }

    private static void MapCompute(RouteGroupBuilder group) =>
        group.MapPost("/{resultSetId:guid}/compute", async (
                Guid resultSetId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new ComputeResultSetCommand(resultSetId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.Compute, ScopeParameterKind.ResultSet, "resultSetId")
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("ComputeResultSet")
            .WithSummary("Compute a result set")
            .WithDescription(
                "Spec 8.2: deletes and rewrites subject_result_line, subject_arm_statistic and " +
                "pupil_term_result from the arm's current marks. Idempotent — running it twice on " +
                "unchanged inputs produces identical rows, and never changes the result set's state. " +
                "Permitted in Draft, Awaiting Approval, Approved and Returned for Correction; refused " +
                "409 once Published or Withdrawn, because a published set renders from its fixed " +
                "snapshot and is never recomputed. `Idempotency-Key` is ACCEPTED, not required — " +
                "computation has no side effect a retry could duplicate.")
            .Produces<ComputeResultSetResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSubmit(RouteGroupBuilder group) =>
        group.MapPost("/{resultSetId:guid}/submit", async (
                Guid resultSetId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new SubmitResultSetCommand(resultSetId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.Submit, ScopeParameterKind.ResultSet, "resultSetId")
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("SubmitResultSet")
            .WithSummary("Submit a result set for approval")
            .WithDescription(
                "Spec 6.7.5/6.7.11: moves Draft or Returned for Correction to Awaiting Approval, " +
                "locking marks, ratings, attendance and the class teacher's remark against editing. " +
                "The head teacher's remark is NEVER a submission blocker — it gates publication only " +
                "(TASK-0088 human ruling). Re-evaluates the completeness gate under the same row lock " +
                "compute and every sheet save take, so a concurrent save either lands first or is " +
                "refused 409 once this commits. 422 `result_set.not_ready` carries the SAME `readiness` " +
                "body `GET /arms/{armId}/readiness` returns for this set, so the screen needs no second " +
                "call. 409 when the state is not Draft or Returned for Correction, or the arm's session " +
                "or term is closed. An unknown id is 403, not 404 (TASK-0071 ruling). " +
                "`Idempotency-Key` is ACCEPTED, not required.")
            .Produces<SubmitResultSetResponse>(StatusCodes.Status200OK)
            .Produces<ResultSetNotReadyProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
