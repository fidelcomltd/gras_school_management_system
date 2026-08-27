using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Reference.Ping;
using SchoolManagement.Application.Reference.SampleRecords;
using SchoolManagement.Domain.Security;

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
        MapSecureArm(group);
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
    /// Reports the calling user's identity. Anonymous by necessity as of TASK-0002 — see the remarks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TASK-0002 CHANGE. Before the boot-time privilege-declaration guard (spec 9.2) existed, this
    /// endpoint deliberately declared NO authorisation metadata of its own, relying solely on the
    /// deny-by-default fallback policy, to prove that policy actually protects a route nobody
    /// remembered to secure. The guard now makes that exact pattern a startup failure: every mapped
    /// route must either call <c>RequirePrivilege(...)</c> or <c>AllowAnonymous()</c> explicitly, so
    /// a "protected only by the bare fallback" endpoint can no longer exist in a running application.
    /// </para>
    /// <para>
    /// Per the TASK-0002 task card's own instruction for exactly this situation ("mark it explicitly
    /// anonymous rather than inventing a privilege for it"), this endpoint is now
    /// <c>.AllowAnonymous()</c>. It still reports whatever identity the request carries — useful once
    /// TASK-0003 wires real authentication — it simply no longer requires one. The deny-by-default
    /// guarantee itself is now proven statically by <c>PrivilegeDeclarationGuardTests</c> (a route
    /// with neither declaration fails to register) and at runtime by
    /// <c>SecurityAndErrorContractTests.AnUnknownRoute_Returns401NotFound_BecauseOfTheFallbackPolicy</c>
    /// (anything unmapped is still denied).
    /// </para>
    /// </remarks>
    private static void MapWhoAmI(RouteGroupBuilder group) =>
        group.MapGet("/whoami", (ICurrentUser currentUser) =>
                TypedResults.Ok(new WhoAmIResponse(currentUser.UserId, currentUser.IsAuthenticated)))
            .AllowAnonymous()
            .WithName("WhoAmI")
            .WithSummary("Return the calling user's identity")
            .WithDescription(
                "Anonymous. Reports whatever identity the request carries — null and false while no " +
                "authentication mechanism is wired (see TASK-0003) — without requiring one.")
            .Produces<WhoAmIResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    /// <summary>
    /// A SCOPABLE PROTECTED endpoint. Exists to prove — and to let the integration tests exercise —
    /// the TASK-0002 privilege substrate: <see cref="PrivilegeRequirementExtensions.RequirePrivilege"/>,
    /// scope resolution rule 1 of spec 4.2.1 (a request naming an arm resolves to that arm), and the
    /// 403 ProblemDetails shape.
    /// </summary>
    /// <remarks>
    /// Copy this call shape — <c>RequirePrivilege(privilege, ScopeParameterKind.Arm, "armId")</c> —
    /// for any future route scoped by an arm named directly in the path. Requires
    /// <see cref="Privileges.Arm.View"/> because it is genuinely read-only and scopable, and it does
    /// not exist as a public product surface — it is reference-slice test scaffolding, wired into the
    /// contract like the rest of this file.
    /// </remarks>
    private static void MapSecureArm(RouteGroupBuilder group) =>
        group.MapGet("/arms/{armId:guid}/secure", (Guid armId) =>
                TypedResults.Ok(new SecureArmResponse(armId)))
            .RequirePrivilege(Privileges.Arm.View, ScopeParameterKind.Arm, "armId")
            .WithName("GetSecureArm")
            .WithSummary("Read an arm-scoped resource, gated by the privilege substrate")
            .WithDescription(
                $"Requires `{Privileges.Arm.View}`, scoped to the arm named in the path. Anonymous " +
                "callers get 401; an authenticated caller who lacks the privilege, or who holds it " +
                "only over a different arm, gets 403 with a generic body naming neither the privilege " +
                "nor whether the arm exists.")
            .Produces<SecureArmResponse>(StatusCodes.Status200OK)
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

/// <summary>The arm-scoped resource <c>GetSecureArm</c> returns once the privilege check passes.</summary>
/// <param name="ArmId">The arm named in the request path — the resolved scope target.</param>
public sealed record SecureArmResponse(Guid ArmId);
