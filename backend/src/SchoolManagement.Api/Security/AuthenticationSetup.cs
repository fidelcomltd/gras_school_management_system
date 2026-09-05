using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Application.Abstractions.Identity;

namespace SchoolManagement.Api.Security;

/// <summary>
/// Wires authentication. See <see cref="AuthenticationModes"/> for the mode this branches on.
/// </summary>
public static class AuthenticationSetup
{
    /// <summary>The placeholder scheme's name.</summary>
    public const string PlaceholderScheme = "Placeholder";

    /// <summary>TASK-0003's real scheme name — see <see cref="CookieSessionAuthenticationHandler"/>.</summary>
    public const string CookieSessionScheme = "CookieSession";

    /// <summary>
    /// Registers the configured authentication scheme and the authorisation policies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="validateOnStart">
    /// Whether to enforce startup guards. Pass <c>false</c> only in contract-generation mode, where the
    /// process exists solely to emit the OpenAPI document — see <see cref="Configuration.HostMode"/>.
    /// </param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// The refusal to run the placeholder scheme outside Development lives in
    /// <see cref="StartupEnvironmentGuard"/>, not here. Throwing during service registration would
    /// break OpenAPI document generation, which starts the host to read its endpoints.
    /// </remarks>
    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        bool validateOnStart = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddValidatedOptions<ApiAuthenticationOptions, ApiAuthenticationOptionsValidator>(
            configuration,
            ApiAuthenticationOptions.SectionName,
            validateOnStart);

        if (validateOnStart)
        {
            services.AddHostedService<StartupEnvironmentGuard>();
        }

        // Read directly rather than through IOptions: authentication schemes must be registered
        // before the service provider exists, matching how CORS/RateLimiting sections are read in
        // Program.cs.
        var mode = configuration
            .GetSection(ApiAuthenticationOptions.SectionName)
            .Get<ApiAuthenticationOptions>()?.Mode ?? AuthenticationModes.Placeholder;

        if (string.Equals(mode, AuthenticationModes.CookieSession, StringComparison.Ordinal))
        {
            services
                .AddAuthentication(CookieSessionScheme)
                .AddScheme<CookieSessionAuthenticationSchemeOptions, CookieSessionAuthenticationHandler>(
                    CookieSessionScheme,
                    displayName: "Cookie session (TASK-0003)",
                    configureOptions: null);
        }
        else
        {
            services
                .AddAuthentication(PlaceholderScheme)
                .AddScheme<AuthenticationSchemeOptions, PlaceholderAuthenticationHandler>(
                    PlaceholderScheme,
                    displayName: "Placeholder (no authentication configured)",
                    configureOptions: null);
        }

        // Backs CsrfTokenService's authenticated tokens — see that class's remarks for why Data
        // Protection stands in for a hand-rolled HMAC-over-a-secret construction.
        //
        // SetApplicationName is set explicitly (second-pass review MEDIUM 5) so the key ring is keyed
        // to a stable, deployment-independent name rather than Data Protection's own default (derived
        // from the content root path), which can differ between two processes that are supposed to be
        // the same application — e.g. two container instances built from the same image but started
        // with a different working directory.
        //
        // KEY PERSISTENCE IS DELIBERATELY NOT CONFIGURED HERE. With no explicit persistence provider,
        // ASP.NET Core stores keys wherever its default applies for the current host (a local
        // directory, or an ephemeral in-memory ring in some container/hosting scenarios) — meaning a
        // second replica, or a restarted process without a mounted, persisted key path, will not share
        // or retain keys. Every outstanding CSRF token becomes unverifiable when that happens (a
        // clean, generic 403 csrf.invalid — not a security hole, just a forced re-fetch of
        // /auth/csrf). This is genuinely a DEPLOYMENT decision (where keys live, whether there is more
        // than one replica) and Open question 5 (production target) is unresolved, so it is tracked as
        // accepted drift rather than guessed at here. Configure a persistence provider
        // (`PersistKeysToFileSystem`, `PersistKeysToAzureBlobStorage`, etc.) once that decision lands.
        services.AddDataProtection().SetApplicationName("SchoolManagement");
        services.AddSingleton<CsrfTokenService>();
        services.AddScoped<ICurrentSession, HttpCurrentSession>();

        services.AddAuthorization(AuthorizationPolicies.Configure);

        return services;
    }
}

/// <summary>
/// An authentication handler that never authenticates anyone.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS EXISTS RATHER THAN NO SCHEME AT ALL: with the deny-by-default fallback policy in place,
/// ASP.NET Core needs a registered scheme to issue a challenge. With none, a protected endpoint throws
/// <c>InvalidOperationException("No authenticationScheme was specified")</c> and returns 500 — which
/// looks like a bug rather than the intended "you are not signed in". This handler makes the
/// authorisation pipeline behave CORRECTLY (a clean 401) while the identity provider is undecided, so
/// the deny-by-default guarantee is genuinely tested rather than assumed.
/// </para>
/// <para>
/// Returning <see cref="AuthenticateResult.NoResult"/> rather than a failure is deliberate: there is no
/// credential to reject, the request is simply anonymous.
/// </para>
/// </remarks>
internal sealed class PlaceholderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        // No credentials are ever recognised. Anonymous endpoints work; protected ones return 401.
        Task.FromResult(AuthenticateResult.NoResult());
}

/// <summary>
/// <see cref="Application.Abstractions.Identity.ICurrentUser"/> over <c>HttpContext.User</c>.
/// </summary>
/// <remarks>
/// Reads <see cref="ClaimTypes.NameIdentifier"/>, falling back to the OpenID Connect <c>sub</c> claim,
/// which is what most identity providers actually emit. Returns <c>null</c> rather than a placeholder
/// string when there is no identity, so audit columns distinguish "anonymous" from a user literally
/// named "system".
/// </remarks>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    : Application.Abstractions.Identity.ICurrentUser
{
    /// <inheritdoc />
    public string? UserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;

            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var identifier = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub");

            return string.IsNullOrWhiteSpace(identifier) ? null : identifier;
        }
    }

    /// <inheritdoc />
    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
