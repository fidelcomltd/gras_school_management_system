using SchoolManagement.Api.Http;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Security.PrivilegeRegister;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// The privilege register (TASK-0028 dispatch 1; spec 4.4; spec 6.1.14). Approved contract delta:
/// <c>.agent/decisions/2026-Q3-contract-deltas.md</c>, entry <c>TASK-0028</c>, section 1 — build
/// exactly what it specifies.
/// </summary>
/// <remarks>
/// <c>GET /privileges</c> uses <c>.RequireAuthenticatedCaller()</c>, not
/// <c>.RequirePrivilege(...)</c>: spec 6.1.14 states explicitly that no privilege gates this read,
/// since every signed-in admin — whatever role they hold — needs to see the full register to
/// understand what a role can be built from. See <see cref="AuthenticatedCallerRequirementExtensions"/>.
/// </remarks>
public sealed class PrivilegesEndpoints : IEndpointModule
{
    private const string Tag = "Privileges";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/privileges")
            .WithTags(Tag);

        MapGetPrivilegeRegister(group);
    }

    private static void MapGetPrivilegeRegister(RouteGroupBuilder group) =>
        group.MapGet(string.Empty, async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new GetPrivilegeRegisterQuery(), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequireAuthenticatedCaller()
            .WithName("GetPrivilegeRegister")
            .WithSummary("Read the privilege register, grouped by module")
            .WithDescription(
                "Every privilege the system understands (spec 4.4), grouped 4.4.1 through 4.4.6 in " +
                "spec table order. Authenticated only — spec 6.1.14 requires no specific privilege " +
                "to read this: every signed-in admin needs to see the full menu of privileges to " +
                "understand what a role can be built from. NOT paged: this is a fixed, " +
                "compile-time, 93-row register, not a growing list, so the usual cursor-pagination " +
                "rule (spec 9.5) does not apply. Legacy `guardian.*` aliases never appear here — " +
                "canonical codes only.")
            .Produces<PrivilegeRegisterResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
