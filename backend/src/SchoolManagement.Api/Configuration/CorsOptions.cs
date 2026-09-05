using Microsoft.Extensions.Options;
using SchoolManagement.Api.Security;

namespace SchoolManagement.Api.Configuration;

/// <summary>
/// Cross-origin policy, bound from the <c>Cors</c> section.
/// </summary>
/// <remarks>
/// Origins are an EXPLICIT ALLOW-LIST from configuration. There is no wildcard option and no
/// "allow any origin in development" shortcut, because a permissive development default is exactly
/// what gets promoted to production by accident.
/// </remarks>
public sealed class CorsOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Cors";

    /// <summary>The named CORS policy applied to the API.</summary>
    public const string PolicyName = "SchoolManagementApiCors";

    /// <summary>
    /// Exact origins permitted to call the API, as scheme + host + optional port with no trailing
    /// slash — for example <c>https://app.example.com</c>.
    /// </summary>
    /// <remarks>
    /// A get-only <see cref="IList{T}"/> rather than a settable array: the configuration binder adds
    /// into the existing instance, and returning an array from a property would hand every caller a
    /// mutable copy of internal state (CA1819).
    /// </remarks>
    public IList<string> AllowedOrigins { get; } = [];

    /// <summary>
    /// Whether the browser may send credentials (cookies, <c>Authorization</c>) cross-origin.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c> because the intended client is a first-party web app. If the auth
    /// decision lands on bearer tokens held in memory rather than cookies, set this to <c>false</c> —
    /// it must be a deliberate choice, and it must match the frontend's <c>credentials</c> mode
    /// exactly or every cross-origin request will fail in a way that looks like a CORS misconfiguration.
    /// </remarks>
    public bool AllowCredentials { get; set; } = true;

    /// <summary>How long a browser may cache the preflight response, in seconds. Default 10 minutes.</summary>
    public int PreflightMaxAgeSeconds { get; set; } = 600;
}

/// <summary>Validates <see cref="CorsOptions"/> at startup.</summary>
/// <remarks>
/// Cross-checks against <see cref="ApiAuthenticationOptions"/> (second-pass review HIGH 3): CLAUDE.md
/// §5's "one coherent set" was previously only a comment, and the shipped configuration itself
/// contradicted it (<c>AllowCredentials: false</c> while <c>Authentication:Mode</c> was
/// <c>CookieSession</c>) — exactly the failure mode a browser reports as a silent CORS error rather
/// than a clear one, so it needs mechanical enforcement rather than a reviewer catching it by reading.
/// </remarks>
internal sealed class CorsOptionsValidator(IOptions<ApiAuthenticationOptions> authenticationOptions)
    : IValidateOptions<CorsOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, CorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        var key = $"{CorsOptions.SectionName}:{nameof(CorsOptions.AllowedOrigins)}";

        foreach (var origin in options.AllowedOrigins)
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                failures.Add($"'{key}' contains an empty entry.");
                continue;
            }

            // THE IMPORTANT CHECK. Browsers reject "Access-Control-Allow-Origin: *" together with
            // credentials, but ASP.NET Core will happily let you configure the combination and the
            // failure then appears as an unexplained CORS error in the browser. Worse, a wildcard with
            // credentials disabled still exposes every endpoint to every site on the internet.
            if (origin.Contains('*', StringComparison.Ordinal))
            {
                failures.Add(
                    $"'{key}' contains the wildcard '{origin}'. Wildcard origins are not permitted; " +
                    "list each origin explicitly.");
                continue;
            }

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                failures.Add(
                    $"'{key}' entry '{origin}' is not an absolute http/https URL. Expected a value " +
                    "like 'https://app.example.com'.");
                continue;
            }

            if (!string.IsNullOrEmpty(uri.AbsolutePath.TrimEnd('/')))
            {
                failures.Add(
                    $"'{key}' entry '{origin}' must not include a path. An origin is scheme, host and " +
                    "port only.");
            }
        }

        if (options.AllowCredentials && options.AllowedOrigins.Count == 0)
        {
            failures.Add(
                $"'{CorsOptions.SectionName}:{nameof(CorsOptions.AllowCredentials)}' is enabled but " +
                $"'{key}' is empty. Credentialed cross-origin requests require an explicit origin " +
                "list. If this service is called only from the same origin, set AllowCredentials to " +
                "false.");
        }

        if (!options.AllowCredentials &&
            string.Equals(
                authenticationOptions.Value.Mode,
                AuthenticationModes.CookieSession,
                StringComparison.Ordinal))
        {
            failures.Add(
                $"'{CorsOptions.SectionName}:{nameof(CorsOptions.AllowCredentials)}' is false while " +
                $"'{ApiAuthenticationOptions.SectionName}:{nameof(ApiAuthenticationOptions.Mode)}' is " +
                "'CookieSession' (root CLAUDE.md §5: CORS, SameSite/Secure and credentials mode are " +
                "one coherent set). A browser silently discards the session cookie on a credentialed " +
                "cross-origin response unless the server echoes " +
                "'Access-Control-Allow-Credentials: true' — sign-in would appear to succeed and then " +
                "every subsequent request would be anonymous. Set AllowCredentials to true.");
        }

        if (options.PreflightMaxAgeSeconds is < 0 or > 86_400)
        {
            failures.Add(
                $"'{CorsOptions.SectionName}:{nameof(CorsOptions.PreflightMaxAgeSeconds)}' must be " +
                $"between 0 and 86400, but was {options.PreflightMaxAgeSeconds}.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
