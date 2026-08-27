using Microsoft.AspNetCore.Authorization;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Api.Security;

/// <summary>
/// One route's declared privilege and, where scopable, which route parameter resolves the target
/// arm (spec 9.2). Attached to an endpoint by <see cref="PrivilegeRequirementExtensions.RequirePrivilege"/>.
/// </summary>
/// <remarks>
/// Validated in the CONSTRUCTOR, not deferred to first request. A route is mapped once, at process
/// startup, so a bad declaration (an unknown privilege code, a scope parameter on a non-scopable
/// privilege, a missing parameter name) throws immediately when the endpoint module runs — which is
/// exactly the "startup failure rather than a hole in production" spec 9.2 asks for.
/// </remarks>
internal sealed class PrivilegeRequirement : IAuthorizationRequirement
{
    public PrivilegeRequirement(string privilege, ScopeParameterKind scopeParameterKind, string? routeParameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privilege);

        if (!PrivilegeRegistry.Exists(privilege))
        {
            throw new ArgumentException(
                $"'{privilege}' is not in the privilege register (SchoolManagement.Domain.Security." +
                $"Privileges). A route cannot require a privilege that does not exist.",
                nameof(privilege));
        }

        var scopable = PrivilegeRegistry.IsScopable(privilege);

        if (scopeParameterKind != ScopeParameterKind.None)
        {
            if (!scopable)
            {
                throw new ArgumentException(
                    $"'{privilege}' is not scopable, so it cannot declare a scope parameter. Use " +
                    $"{nameof(ScopeParameterKind)}.{nameof(ScopeParameterKind.None)} for a " +
                    "non-scopable privilege.",
                    nameof(scopeParameterKind));
            }

            if (scopeParameterKind != ScopeParameterKind.Level && string.IsNullOrWhiteSpace(routeParameterName))
            {
                throw new ArgumentException(
                    "A route parameter name is required when the scope parameter kind resolves an " +
                    "arm, a pupil, or a result set.",
                    nameof(routeParameterName));
            }
        }

        Privilege = privilege;
        ScopeParameterKind = scopeParameterKind;
        RouteParameterName = routeParameterName;
    }

    /// <summary>The canonical privilege code the route requires.</summary>
    public string Privilege { get; }

    /// <summary>What the route's scope-bearing route parameter names, per spec 4.2.1.</summary>
    public ScopeParameterKind ScopeParameterKind { get; }

    /// <summary>
    /// The route parameter to read for scope resolution, or <see langword="null"/> when
    /// <see cref="ScopeParameterKind"/> is <see cref="ScopeParameterKind.None"/> or
    /// <see cref="ScopeParameterKind.Level"/> (a level-only route has no arm parameter to read).
    /// </summary>
    public string? RouteParameterName { get; }
}
