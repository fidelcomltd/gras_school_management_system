using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Http;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Reference.Ping;
using SchoolManagement.Application.Reference.SampleRecords;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// REFERENCE SCAFFOLD — the endpoint module every future module should be copied from.
/// </summary>
/// <remarks>
/// <para>
/// Delete this file with the <c>SampleRecord</c> slice. Until then it is the executable specification
/// for "how do I add an endpoint here": read it top to bottom before writing your first one.
/// </para>
/// <para>
/// EVERY ENDPOINT BELOW DEMONSTRATES THE SAME FIVE THINGS:
/// </para>
/// <list type="number">
/// <item>A body that only dispatches and matches — no logic, no data access, no status codes.</item>
/// <item>A <see cref="CancellationToken"/> parameter, which Minimal APIs binds to
/// <c>HttpContext.RequestAborted</c> and which flows to EF Core.</item>
/// <item>Complete OpenAPI metadata: summary, description, operation ID, and EVERY response
/// type — because the committed contract is generated from exactly this.</item>
/// <item>An explicit authorisation decision. <c>.AllowAnonymous()</c> is a visible choice; omitting it
/// means the deny-by-default fallback policy protects the endpoint.</item>
/// <item>A rate-limiting policy.</item>
/// </list>
/// </remarks>
public sealed class ReferenceEndpoints : IEndpointModule
{
    /// <summary>OpenAPI tag grouping these endpoints in the generated document and docs UI.</summary>
    private const string Tag = "Reference";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/reference")
            .WithTags(Tag);

        MapPing(group);
        MapListSampleRecords(group);
        MapCreateSampleRecord(group);
        MapWhoAmI(group);
    }

    /// <summary>The simplest possible endpoint: a query with no persistence.</summary>
    private static void MapPing(RouteGroupBuilder group) =>
        group.MapGet("/ping", async (
                [FromQuery] string name,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new PingQuery(name), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .AllowAnonymous()
            .WithName("Ping")
            .WithSummary("Check that the API is reachable")
            .WithDescription(
                "Echoes the supplied name with the server's UTC time and the API version. Useful as a " +
                "smoke test of routing, serialisation and the request pipeline. Requires no " +
                "authentication and touches no database.")
            .Produces<PingResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>A paginated read. Copy this for any collection endpoint.</summary>
    private static void MapListSampleRecords(RouteGroupBuilder group) =>
        group.MapGet("/records", async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] int? page = null,
                [FromQuery] int? pageSize = null) =>
            {
                var result = await sender.SendAsync(
                    new ListSampleRecordsQuery(page, pageSize),
                    cancellationToken);

                return result.Match(TypedResults.Ok);
            })
            .AllowAnonymous()
            .WithName("ListSampleRecords")
            .WithSummary("List sample records, newest first")
            .WithDescription(
                "Returns one page of records in the standard pagination envelope. `page` is 1-based and " +
                "defaults to 1; `pageSize` defaults to 20 and is capped at 100. A `pageSize` above the " +
                "cap is REJECTED with 422 rather than silently reduced, so a client paging through " +
                "results cannot skip rows while believing it read everything.")
            .Produces<PagedResult<SampleRecordDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>A command. Note the 201 + Location, and the 409 for a conflict.</summary>
    private static void MapCreateSampleRecord(RouteGroupBuilder group) =>
        group.MapPost("/records", async (
                CreateSampleRecordCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);

                // 201 with a Location header pointing at the created resource. The response body also
                // carries the id so a client need not parse the URL.
                return result.Match(response => TypedResults.Created(
                    $"/api/v1/reference/records/{response.Id}",
                    response));
            })
            .AllowAnonymous()
            .WithName("CreateSampleRecord")
            .WithSummary("Create a sample record")
            .WithDescription(
                "Creates a record and returns 201 with a `Location` header. The label must be unique " +
                "among live (not soft-deleted) records; a duplicate returns 409. Runs inside a " +
                "transaction that is rolled back if the command fails.")
            .Produces<CreateSampleRecordResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// A PROTECTED endpoint. Exists to prove the deny-by-default authorisation pipeline actually works.
    /// </summary>
    /// <remarks>
    /// Note there is no <c>.AllowAnonymous()</c> and no <c>[Authorize]</c> either — the fallback policy
    /// protects it. That is the guarantee under test: an endpoint nobody remembered to secure is still
    /// secured. The integration test asserts this returns 401 while the placeholder authentication
    /// scheme is in effect.
    /// </remarks>
    private static void MapWhoAmI(RouteGroupBuilder group) =>
        group.MapGet("/whoami", (ICurrentUser currentUser) =>
                TypedResults.Ok(new WhoAmIResponse(currentUser.UserId, currentUser.IsAuthenticated)))
            .WithName("WhoAmI")
            .WithSummary("Return the calling user's identity")
            .WithDescription(
                "Requires authentication. Included to demonstrate — and test — that endpoints are " +
                "protected by default: this endpoint declares no authorisation metadata of its own and " +
                "is secured by the fallback policy. While no identity provider is configured it always " +
                "returns 401.")
            .Produces<WhoAmIResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}

/// <summary>The calling user's identity, as the API sees it.</summary>
/// <param name="UserId">
/// The caller's stable identifier, or <c>null</c> when the request is anonymous.
/// </param>
/// <param name="IsAuthenticated">Whether the request carried an authenticated identity.</param>
public sealed record WhoAmIResponse(string? UserId, bool IsAuthenticated);
