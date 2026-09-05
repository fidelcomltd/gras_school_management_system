namespace SchoolManagement.Api.Security;

/// <summary>
/// Marks an endpoint as declaring "authenticated caller required, no privilege gate" — the ONE
/// category <see cref="PrivilegeDeclarationGuard"/> did not previously recognise. TASK-0003 introduces
/// it for <c>GET /auth/me</c>, <c>POST /auth/refresh</c> and <c>POST /auth/password</c>: each is about
/// the caller's OWN account/session, never an RBAC-gated business operation, so
/// <c>RequirePrivilege(...)</c> would mean inventing a privilege that does not exist in spec 4.4's
/// register just to satisfy the guard.
/// </summary>
internal sealed class AuthenticatedCallerRequirementMarker;

/// <summary>Attaches <see cref="AuthenticatedCallerRequirementMarker"/> and the authenticated-user policy.</summary>
internal static class AuthenticatedCallerRequirementExtensions
{
    /// <summary>
    /// Requires any authenticated caller, with no privilege check — see the class remarks for when
    /// this is the right call instead of <see cref="PrivilegeRequirementExtensions.RequirePrivilege"/>.
    /// </summary>
    public static TBuilder RequireAuthenticatedCaller<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(new AuthenticatedCallerRequirementMarker()));

        return builder.RequireAuthorization(AuthorizationPolicies.RequireAuthenticatedUser);
    }
}
