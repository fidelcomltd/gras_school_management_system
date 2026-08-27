using Microsoft.AspNetCore.Authorization;

namespace SchoolManagement.Api.Security;

/// <summary>
/// Registers the TASK-0002 authorization substrate: the privilege requirement handler and the
/// ProblemDetails-shaped forbidden response. Separate from <see cref="AuthenticationSetup"/>
/// because it has nothing to do with WHO the caller is, only what they may do.
/// </summary>
public static class AuthorizationSetup
{
    /// <summary>Adds the services <see cref="PrivilegeRequirementExtensions.RequirePrivilege"/> depends on.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAuthorizationHandler, PrivilegeAuthorizationHandler>();

        // Registered AFTER AddAuthorization/AddAuthentication (both called from
        // AuthenticationSetup.AddApiAuthentication, which must run before this) so it wins over the
        // framework's default registration when the container resolves a single instance.
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationMiddlewareResultHandler>();

        return services;
    }
}
