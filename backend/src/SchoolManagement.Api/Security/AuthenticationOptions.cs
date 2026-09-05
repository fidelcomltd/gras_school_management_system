using Microsoft.Extensions.Options;

namespace SchoolManagement.Api.Security;

/// <summary>
/// Which authentication mechanism the service uses, bound from the <c>Authentication</c> section.
/// </summary>
/// <remarks>
/// The mechanism is a PENDING HUMAN DECISION — see <c>docs/ASSUMPTIONS.md</c> and the orchestrator's
/// open questions. This options type exists so the decision has one obvious place to land, and so the
/// scaffold can refuse to run in a deployed environment until it is made.
/// </remarks>
public sealed class ApiAuthenticationOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Authentication";

    /// <summary>
    /// The authentication mode. Only <see cref="AuthenticationModes.Placeholder"/> is implemented.
    /// </summary>
    public string Mode { get; set; } = AuthenticationModes.Placeholder;
}

/// <summary>The supported values for <see cref="ApiAuthenticationOptions.Mode"/>.</summary>
public static class AuthenticationModes
{
    /// <summary>
    /// No real authentication. Every request is anonymous, so protected endpoints return 401.
    /// </summary>
    /// <remarks>
    /// This is a SCAFFOLD state, not a mechanism. It exists so the deny-by-default authorisation
    /// pipeline is genuinely exercised — including by the integration tests — before an identity
    /// provider is chosen. Startup REFUSES this mode outside Development.
    /// </remarks>
    public const string Placeholder = "Placeholder";

    /// <summary>
    /// HttpOnly cookie session + CSRF token (TASK-0003). The mechanism root CLAUDE.md §5 recommends
    /// for a first-party web app, human sign-off 2026-08-26. Implemented by
    /// <see cref="CookieSessionAuthenticationHandler"/>.
    /// </summary>
    public const string CookieSession = "CookieSession";

    /// <summary>Bearer JWT with refresh. NOT IMPLEMENTED.</summary>
    public const string BearerJwt = "BearerJwt";
}

/// <summary>Validates <see cref="ApiAuthenticationOptions"/> at startup.</summary>
internal sealed class ApiAuthenticationOptionsValidator : IValidateOptions<ApiAuthenticationOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ApiAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string[] known =
        [
            AuthenticationModes.Placeholder,
            AuthenticationModes.CookieSession,
            AuthenticationModes.BearerJwt,
        ];

        if (!known.Contains(options.Mode, StringComparer.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                $"'{ApiAuthenticationOptions.SectionName}:{nameof(ApiAuthenticationOptions.Mode)}' " +
                $"was '{options.Mode}'. Expected one of: {string.Join(", ", known)}.");
        }

        if (options.Mode is AuthenticationModes.BearerJwt)
        {
            return ValidateOptionsResult.Fail(
                $"Authentication mode '{options.Mode}' is declared but NOT IMPLEMENTED. The chosen " +
                "mechanism (root CLAUDE.md §5, human sign-off 2026-08-26) is CookieSession — see " +
                "docs/ASSUMPTIONS.md.");
        }

        return ValidateOptionsResult.Success;
    }
}
