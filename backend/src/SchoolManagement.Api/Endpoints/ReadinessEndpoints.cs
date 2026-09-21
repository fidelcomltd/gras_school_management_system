using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// The arm's readiness grid — one arm's completeness gate for one term (TASK-0088 stage B; spec
/// §6.7.5, §6.7.11 amendment). Maps routes under the same <c>/arms</c> prefix
/// <see cref="TraitRatingEndpoints"/> and <see cref="AttendanceEndpoints"/> own — a domain split, not a
/// URL-prefix one.
/// </summary>
/// <remarks>
/// ROUTE SHAPE IS ARM-SCOPED, NOT RESULT-SET-SCOPED — departing from spec §6.7.13's literal
/// <c>GET /result-sets/{id}/readiness</c>, for the SAME reason every other sheet route in this module
/// departs: a "Not started" arm (no result set row yet) must still answer, with everything reading as
/// missing, rather than needing an id that does not exist.
/// </remarks>
public sealed class ReadinessEndpoints : IEndpointModule
{
    private const string Tag = "Readiness";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/arms").WithTags(Tag);

        MapGetReadiness(group);
    }

    private static void MapGetReadiness(RouteGroupBuilder group) =>
        group.MapGet("/{armId:guid}/readiness", async (
                Guid armId,
                [FromQuery(Name = "termId")] string termId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetResultSetReadinessQuery(armId.ToString(), termId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.View, ScopeParameterKind.Arm, "armId")
            .WithName("GetResultSetReadiness")
            .WithSummary("Read one arm's completeness gate for a term")
            .WithDescription(
                "Spec 6.7.5/6.7.11: the readiness grid (subjects across the top, pupils down the " +
                "side, marks/ratings/attendance/remarks completeness), the five counters, pupils who " +
                "left the arm during the term, and why submission is blocked, if it is. Answers for a " +
                "Not started arm too — `resultSet: null`, everything reads as missing. The head " +
                "teacher's remark is an informational counter only, never a blocker (TASK-0088 human " +
                "ruling: it gates publication, not submission).")
            .Produces<ResultSetReadinessDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
