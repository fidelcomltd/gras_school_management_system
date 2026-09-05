using Microsoft.AspNetCore.Authorization;

namespace SchoolManagement.Api.Security;

/// <summary>
/// THE BOOT-TIME GUARD. Spec 9.2: "A route with no declared privilege fails to register at boot,
/// so a forgotten check is a startup failure rather than a hole in production."
/// </summary>
/// <remarks>
/// <para>
/// Runs synchronously, right after every endpoint is mapped, with no external dependency — unlike
/// <c>StartupEnvironmentGuard</c> it does not need to be deferred past OpenAPI-document generation
/// (<c>HostMode.IsContractGeneration</c>): there is no database, no configuration, nothing to skip.
/// It runs on every boot, including generation and every test that starts the real host, which is
/// the point — an undeclared route cannot exist in a document or a test run either.
/// </para>
/// <para>
/// A route passes if it is explicitly anonymous (<see cref="IAllowAnonymous"/> metadata — the
/// visible, greppable opt-out spec 9.2 and root CLAUDE.md §5 require), carries a
/// <see cref="PrivilegeRequirement"/> (attached by
/// <see cref="PrivilegeRequirementExtensions.RequirePrivilege"/>), or carries an
/// <see cref="AuthenticatedCallerRequirementMarker"/> (attached by
/// <see cref="AuthenticatedCallerRequirementExtensions.RequireAuthenticatedCaller{TBuilder}"/> —
/// TASK-0003's narrow THIRD category for a route that needs an authenticated caller but checks no
/// privilege, because it is about the caller's own account/session rather than an RBAC-gated
/// business operation: <c>GET /auth/me</c>, <c>POST /auth/refresh</c>, <c>POST /auth/password</c>).
/// Anything else — including a route relying only on the bare deny-by-default fallback policy, with
/// no explicit declaration at all — fails the guard. That is a deliberate tightening over the
/// fallback policy alone: the fallback is what protects a route AT RUNTIME if this guard is ever
/// bypassed, but it is not itself a privilege declaration.
/// </para>
/// </remarks>
internal static class PrivilegeDeclarationGuard
{
    /// <summary>
    /// Validates every endpoint currently mapped onto <paramref name="endpoints"/>.
    /// </summary>
    /// <param name="endpoints">The route builder whose mapped endpoints to check.</param>
    /// <exception cref="InvalidOperationException">
    /// One or more routes declare neither <see cref="IAllowAnonymous"/>, a
    /// <see cref="PrivilegeRequirement"/>, nor an <see cref="AuthenticatedCallerRequirementMarker"/>.
    /// The message names every offending route.
    /// </exception>
    public static void Validate(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var undeclared = endpoints.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null &&
                endpoint.Metadata.GetMetadata<PrivilegeRequirement>() is null &&
                endpoint.Metadata.GetMetadata<AuthenticatedCallerRequirementMarker>() is null)
            .Select(Describe)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (undeclared.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "The following routes declare no privilege and are not explicitly anonymous: " +
            string.Join("; ", undeclared) +
            ". Every route must call RequirePrivilege(...) naming a privilege from " +
            "SchoolManagement.Domain.Security.Privileges, RequireAuthenticatedCaller() for an " +
            "account/session-only route with no privilege to check, or call AllowAnonymous() to opt " +
            "out explicitly (spec 9.2; root CLAUDE.md §5).");
    }

    private static string Describe(RouteEndpoint endpoint) =>
        endpoint.DisplayName ?? endpoint.RoutePattern.RawText ?? "(unnamed route)";
}
