using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Results.Annual;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Endpoints;

/// <summary>Annual cumulative results (spec 6.7.10, 6.7.13).</summary>
public sealed class AnnualResultEndpoints : IEndpointModule
{
    private const string Tag = "Annual results";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGroup("/arms").WithTags(Tag)
            .MapPost("/{armId:guid}/annual-results", async (
                Guid armId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new ComputeAnnualResultsCommand(armId), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequirePrivilege(Privileges.Results.AnnualCompute, ScopeParameterKind.None)
            .RequireCsrfToken()
            .RequireIdempotencyKey(required: false)
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("ComputeAnnualResults")
            .WithSummary("Compute an arm's annual cumulative results")
            .WithDescription(
                "Spec 6.7.10: for every pupil with a result in the arm's Third Term set, the cumulative " +
                "average over the terms they sat (simple or weighted per the result rules), its grade from " +
                "the Third Term snapshot's bands, the annual position in this arm (pupils with one term are " +
                "not ranked), per-subject means, and the proposed promotion outcome (6.3.7). Rewrites the " +
                "rows each run, so it is idempotent. 409 names the first term that is not published; an " +
                "earlier term the arm was never scored in is skipped rather than blocking. `Idempotency-Key` " +
                "is accepted, not required.")
            .Produces<ComputeAnnualResultsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
