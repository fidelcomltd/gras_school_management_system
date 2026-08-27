using Microsoft.AspNetCore.Authorization;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Authorization;

namespace SchoolManagement.Api.Security;

/// <summary>
/// Enforces a <see cref="PrivilegeRequirement"/>: resolves the caller's effective privilege set,
/// resolves the route's scope target, and applies <see cref="PrivilegeDecision.IsAuthorized"/>.
/// </summary>
/// <remarks>
/// <para>
/// Runs for every request against a route mapped with <c>.RequirePrivilege(...)</c>, in server
/// middleware, before the endpoint handler — spec 9.2: "Every privilege check runs on the server,
/// in middleware, before the handler executes." Because this is ordinary ASP.NET Core policy-based
/// authorization, it applies identically to a GET and a POST, which is what makes "object-level
/// checks run on every read as well as every write" (spec 9.2) true by construction rather than by
/// each read handler remembering to filter.
/// </para>
/// <para>
/// A caller who is not authenticated never reaches <see cref="HandleRequirementAsync"/> with a
/// usable identity: the policy also carries <c>RequireAuthenticatedUser()</c> (added by
/// <see cref="PrivilegeRequirementExtensions.RequirePrivilege"/>), and ASP.NET Core resolves an
/// unauthenticated failure as a 401 challenge rather than calling this handler's requirement as
/// forbidden. The <see cref="ICurrentUser.UserId"/> null-check below is defence in depth, not the
/// primary guard.
/// </para>
/// </remarks>
internal sealed class PrivilegeAuthorizationHandler(
    ICurrentUser currentUser,
    IEffectivePrivilegeProvider grantsProvider,
    IScopeResolver scopeResolver,
    IAuthorizationAuditSink auditSink,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PrivilegeRequirement>
{
    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PrivilegeRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (currentUser.UserId is not { } userId)
        {
            // No usable identity — let the DenyAnonymousAuthorizationRequirement (also on this
            // policy) fail the request into a 401 challenge. Neither succeeding nor failing THIS
            // requirement changes that outcome, but failing it is the honest answer.
            context.Fail();
            return;
        }

        var httpContext = httpContextAccessor.HttpContext;
        var cancellationToken = httpContext?.RequestAborted ?? CancellationToken.None;

        var grants = await grantsProvider.GetGrantsAsync(userId, cancellationToken).ConfigureAwait(false);

        var parameterValue = ResolveRouteParameterValue(httpContext, requirement.RouteParameterName);

        var resolution = await scopeResolver
            .ResolveAsync(requirement.ScopeParameterKind, parameterValue, cancellationToken)
            .ConfigureAwait(false);

        if (PrivilegeDecision.IsAuthorized(grants, requirement.Privilege, resolution))
        {
            context.Succeed(requirement);
            return;
        }

        await auditSink
            .RecordRejectionAsync(userId, requirement.Privilege, httpContext?.Request.Path.Value, cancellationToken)
            .ConfigureAwait(false);

        context.Fail();
    }

    /// <summary>
    /// Reads and parses the named route parameter as a <see cref="Guid"/>. Returns
    /// <see langword="null"/> for a missing, empty, or unparsable value — the resolver treats that
    /// identically to "not named", which fails closed rather than guessing.
    /// </summary>
    private static Guid? ResolveRouteParameterValue(HttpContext? httpContext, string? routeParameterName)
    {
        if (httpContext is null || routeParameterName is null)
        {
            return null;
        }

        var raw = httpContext.Request.RouteValues[routeParameterName]?.ToString();

        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }
}
