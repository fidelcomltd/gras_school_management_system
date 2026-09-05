using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SchoolManagement.Api.Http;
using SchoolManagement.Application.Abstractions.Auth;

namespace SchoolManagement.Api.Security;

/// <summary>Options for <see cref="CookieSessionAuthenticationHandler"/>. No settings of its own yet.</summary>
public sealed class CookieSessionAuthenticationSchemeOptions : AuthenticationSchemeOptions;

/// <summary>
/// Validates the <c>__Host-Session</c> cookie against <see cref="IAdminSessionAuthenticator"/> and,
/// on a challenge, writes the three-variant 401 body the approved contract delta requires (§3) —
/// something the framework's default challenge behaviour (empty 401 body) cannot do, which is why
/// this scheme overrides <see cref="HandleChallengeAsync"/> rather than relying on it.
/// </summary>
internal sealed class CookieSessionAuthenticationHandler(
    IOptionsMonitor<CookieSessionAuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IAdminSessionAuthenticator sessionAuthenticator)
    : AuthenticationHandler<CookieSessionAuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    private AdminSessionAuthenticationOutcome? _failureOutcome;

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(AuthCookies.SessionCookieName, out var rawToken) ||
            string.IsNullOrEmpty(rawToken))
        {
            _failureOutcome = AdminSessionAuthenticationOutcome.NotFound;
            return AuthenticateResult.NoResult();
        }

        var result = await sessionAuthenticator
            .AuthenticateAsync(rawToken, Context.RequestAborted)
            .ConfigureAwait(false);

        if (result.Outcome != AdminSessionAuthenticationOutcome.Valid)
        {
            _failureOutcome = result.Outcome;
            return AuthenticateResult.NoResult();
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, result.AccountId.ToString()),
                new Claim(SessionClaimTypes.SessionId, result.SessionId.ToString()),
                new Claim(SessionClaimTypes.MustChangePassword, result.MustChangePassword ? bool.TrueString : bool.FalseString),
            ],
            authenticationType: Scheme.Name);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    /// <inheritdoc />
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var (errorCode, detail) = (_failureOutcome ?? AdminSessionAuthenticationOutcome.NotFound) switch
        {
            AdminSessionAuthenticationOutcome.Revoked => (
                "authentication.session_revoked",
                "Your access has been withdrawn. Contact the school administrator."),

            AdminSessionAuthenticationOutcome.Expired => (
                "authentication.session_expired",
                "Your session has expired. Sign in again."),

            _ => (
                "authentication.required",
                "Sign in to perform this action."),
        };

        Response.StatusCode = StatusCodes.Status401Unauthorized;

        var problemDetailsService = Context.RequestServices.GetRequiredService<IProblemDetailsService>();

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails =
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication required",
                Detail = detail,
                Type = ApiProblem.ToTypeUrn(errorCode),
                Extensions = { ["errorCode"] = errorCode },
            },
        }).ConfigureAwait(false);
    }
}
