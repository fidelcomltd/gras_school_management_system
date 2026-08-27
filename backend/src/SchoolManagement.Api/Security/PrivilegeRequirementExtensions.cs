using SchoolManagement.Application.Abstractions.Authorization;

namespace SchoolManagement.Api.Security;

/// <summary>
/// The one way an endpoint declares the privilege it requires (spec 9.2). Copy this call shape for
/// every protected route; see <c>ReferenceEndpoints</c> for a worked example.
/// </summary>
internal static class PrivilegeRequirementExtensions
{
    /// <summary>
    /// Requires <paramref name="privilege"/>, resolving scope from <paramref name="routeParameterName"/>
    /// when the privilege is scopable.
    /// </summary>
    /// <param name="builder">The endpoint (or route group) to protect.</param>
    /// <param name="privilege">
    /// A constant from <c>SchoolManagement.Domain.Security.Privileges</c>. Any other string throws
    /// at registration time — see <see cref="PrivilegeRequirement"/>.
    /// </param>
    /// <param name="scopeParameterKind">
    /// What <paramref name="routeParameterName"/> names, per spec 4.2.1. Leave as
    /// <see cref="ScopeParameterKind.None"/> for a non-scopable privilege.
    /// </param>
    /// <param name="routeParameterName">
    /// The route parameter (for example <c>"armId"</c>) that resolves the target arm. Required
    /// whenever <paramref name="scopeParameterKind"/> is <see cref="ScopeParameterKind.Arm"/>,
    /// <see cref="ScopeParameterKind.Pupil"/> or <see cref="ScopeParameterKind.ResultSet"/>.
    /// </param>
    /// <typeparam name="TBuilder">The endpoint convention builder type, for fluent chaining.</typeparam>
    /// <remarks>
    /// Also attaches <paramref name="privilege"/>'s requirement directly as endpoint metadata (in
    /// addition to folding it into the authorization policy), so
    /// <see cref="PrivilegeDeclarationGuard"/> can find it by a simple metadata lookup rather than
    /// by inspecting <c>AuthorizationPolicy</c> internals.
    /// </remarks>
    public static TBuilder RequirePrivilege<TBuilder>(
        this TBuilder builder,
        string privilege,
        ScopeParameterKind scopeParameterKind = ScopeParameterKind.None,
        string? routeParameterName = null)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var requirement = new PrivilegeRequirement(privilege, scopeParameterKind, routeParameterName);

        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(requirement));

        return builder.RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(requirement));
    }
}
