using SchoolManagement.Api.Http;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Pupils;

namespace SchoolManagement.Api.Endpoints;

/// <summary>Reference geography for the pupil forms (spec 6.5.4): the only states and LGAs the server accepts.</summary>
public sealed class GeographyEndpoints : IEndpointModule
{
    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/geography/states", async (ISender sender, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                // Changes only with a deploy; an hour in the browser's private cache saves every form a round trip.
                httpContext.Response.Headers.CacheControl = "private, max-age=3600";
                return (await sender.SendAsync(new GetNigerianGeographyQuery(), cancellationToken)).Match(TypedResults.Ok);
            })
            .RequireAuthenticatedCaller()
            .WithTags("Reference")
            .WithName("GetNigerianGeography")
            .WithSummary("The states and LGAs a pupil record accepts")
            .WithDescription(
                "Spec 6.5.4: the 36 states and the FCT, each with its LGAs, in the canonical spellings the server stores. A pupil's " +
                "state of origin and LGA must come from this list. Any signed-in account; `Cache-Control: private, max-age=3600`.")
            .Produces<NigerianGeographyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
