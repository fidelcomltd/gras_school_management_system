using Microsoft.AspNetCore.Authorization;

namespace SchoolManagement.Api.Security;

/// <summary>
/// Every authorisation policy in the application, named in one place.
/// </summary>
/// <remarks>
/// <para>
/// POLICIES, NOT ROLE STRINGS. <c>[Authorize(Roles = "Admin")]</c> scattered across endpoints cannot
/// be audited ("who can do this?" requires grepping), cannot be changed without touching every call
/// site, and typos in it fail OPEN — a misspelled role name silently matches nobody, but a misspelled
/// policy name throws at startup.
/// </para>
/// <para>
/// Adding a policy: add a <c>const</c> here, register it in <see cref="Configure"/>, then reference the
/// constant from the endpoint. Never inline a policy name as a literal.
/// </para>
/// </remarks>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Requires any authenticated caller. This is also the FALLBACK policy, so it applies to every
    /// endpoint that does not state otherwise.
    /// </summary>
    public const string RequireAuthenticatedUser = "RequireAuthenticatedUser";

    /// <summary>
    /// Configures the authorisation system, including the deny-by-default fallback.
    /// </summary>
    /// <param name="options">The authorisation options to configure.</param>
    public static void Configure(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(
            RequireAuthenticatedUser,
            policy => policy.RequireAuthenticatedUser());

        // ══ THE MOST IMPORTANT THREE LINES IN THIS FILE ══
        // The fallback policy applies to any endpoint with NO authorisation metadata of its own. With
        // it, forgetting to protect a new endpoint makes it return 401 — visible immediately, in the
        // first test anyone writes. Without it, forgetting leaves the endpoint PUBLIC, and nothing
        // fails; you discover it when someone else does. Anonymous access must be opted into
        // explicitly with .AllowAnonymous(), which is greppable and shows up in review.
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        // DefaultPolicy applies to [Authorize] with no policy named. Kept identical to the fallback so
        // there is no gap between "protected by default" and "explicitly protected".
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    }
}
