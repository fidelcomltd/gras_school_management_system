using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Auth;
using SchoolManagement.Application.Auth.ChangePassword;
using SchoolManagement.Application.Auth.MyAccount;
using SchoolManagement.Application.Auth.Refresh;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Auth.SignOut;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Authentication and session management (TASK-0003; spec 6.1.11, 6.1.14; spec 9.1). Approved
/// contract delta: <c>.agent/decisions/2026-Q3-contract-deltas.md</c> § TASK-0003 — build exactly
/// what it specifies; this file's job is to match it, not to reinterpret it.
/// </summary>
/// <remarks>
/// <para>
/// EVERY MUTATING ENDPOINT HERE CALLS <c>.RequireCsrfToken()</c>. That is CLAUDE.md §5's "CSRF token
/// required on every mutating request, implemented once in the API layer, never per-endpoint" —
/// the "once" is <see cref="CsrfEndpointFilterExtensions.RequireCsrfToken{TBuilder}"/> itself; each
/// call site here is just applying it, not re-deriving it.
/// </para>
/// <para>
/// COOKIES ARE SET/CLEARED HERE, NOT IN THE APPLICATION LAYER. A handler cannot touch
/// <see cref="HttpContext"/> (<c>DependencyDirectionTests</c>), so the raw session token a successful
/// sign-in/password-change produces travels back through the command's result ONLY so this endpoint
/// body can turn it into a cookie — it is never part of <see cref="AuthSessionResponse"/> and never
/// serialised into the response body.
/// </para>
/// </remarks>
public sealed class AuthEndpoints : IEndpointModule
{
    private const string Tag = "Auth";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/auth")
            .WithTags(Tag);

        MapGetCsrfToken(group);
        MapSignIn(group);
        MapSignOut(group);
        MapMe(group);
        MapRefresh(group);
        MapChangePassword(group);
    }

    private static void MapGetCsrfToken(RouteGroupBuilder group) =>
        group.MapGet("/csrf", (
                HttpContext httpContext,
                CsrfTokenService csrf,
                ICurrentSession currentSession,
                TimeProvider timeProvider) =>
            {
                var now = timeProvider.GetUtcNow();

                // Second-pass review HIGH 1: the frontend calls this on every app load, INCLUDING a
                // reload while a session is already live — its own description below says exactly
                // that. Rebinding unconditionally to the anonymous subject would then break every
                // subsequent mutation (refresh, password, sign-out) on that session until it expires.
                // Bind to the CURRENT session when one is live, matching sign-in's own binding.
                if (currentSession.SessionId is { } sessionId)
                {
                    var sessionBoundToken = csrf.Issue(sessionId.ToString(), now + TimeSpan.FromHours(8));
                    AuthCookies.SetSessionBoundCsrf(httpContext, sessionBoundToken);
                    return TypedResults.Ok(new CsrfTokenResponse(sessionBoundToken));
                }

                var token = csrf.Issue(CsrfTokenService.AnonymousSubject, now + TimeSpan.FromHours(1));

                AuthCookies.SetAnonymousCsrf(httpContext, token);

                return TypedResults.Ok(new CsrfTokenResponse(token));
            })
            .AllowAnonymous()
            .WithName("GetCsrfToken")
            .WithSummary("Issue a CSRF token")
            .WithDescription(
                "Sets the `__Host-XSRF-TOKEN` cookie and returns the same value in the response body. " +
                "Call on app load, including a reload while already signed in — the cookie is bound to " +
                "the caller's current session when one is live, and to an anonymous subject otherwise, " +
                "so this never invalidates an existing session's ability to mutate. Also call it once " +
                "before sign-in: sign-in is itself CSRF-protected, so this is what bootstraps the pair. " +
                "Echo the returned value verbatim in an `X-CSRF-Token` header on every subsequent " +
                "mutating `/auth/*` request; axios does this automatically via its " +
                "`xsrfCookieName`/`xsrfHeaderName` configuration.")
            .Produces<CsrfTokenResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSignIn(RouteGroupBuilder group) =>
        group.MapPost("/sign-in", async (
                SignInCommand command,
                HttpContext httpContext,
                ISender sender,
                CsrfTokenService csrf,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);

                return result.Match(signInResult =>
                {
                    AuthCookies.SetSession(httpContext, signInResult.RawSessionToken);

                    var csrfExpiresAt = timeProvider.GetUtcNow() + TimeSpan.FromHours(8);
                    var csrfToken = csrf.Issue(signInResult.SessionId.ToString(), csrfExpiresAt);
                    AuthCookies.SetSessionBoundCsrf(httpContext, csrfToken);

                    return TypedResults.Ok(signInResult.Session);
                });
            })
            .AllowAnonymous()
            .RequireCsrfToken()
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("SignIn")
            .WithSummary("Sign in with email and password")
            .WithDescription(
                "Requires an `X-CSRF-Token` header matching the `__Host-XSRF-TOKEN` cookie from " +
                "`GET /auth/csrf`. On success, sets the `__Host-Session` cookie and rotates the CSRF " +
                "cookie to one bound to the new session. Rate-limited under the sensitive policy " +
                "(spec 6.1.11). A wrong password, an unknown email, and a locked account given a wrong " +
                "password all return the identical generic 401 body — `423` fires ONLY when the " +
                "submitted password is correct and the account is currently locked, so only the real " +
                "account holder ever learns that.")
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapSignOut(RouteGroupBuilder group) =>
        group.MapPost("/sign-out", async (
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new SignOutCommand(), cancellationToken);

                return result.Match(() =>
                {
                    AuthCookies.ClearSession(httpContext);
                    AuthCookies.ClearCsrf(httpContext);
                    return TypedResults.NoContent();
                });
            })
            .AllowAnonymous()
            // Second-pass review MEDIUM 4: tolerant of a CSRF cookie bound to a session that has since
            // expired (idle timeout 30 min, cookie lifetime 8h) — the caller is no longer authenticated
            // by then, so the strict check would compare that session-bound subject against "anon" and
            // reject a legitimate sign-out. See RequireCsrfToken's and ValidateIgnoringSubject's remarks.
            .RequireCsrfToken(tolerateDeadSessionSubjectMismatch: true)
            .WithName("SignOut")
            .WithSummary("Sign out")
            .WithDescription(
                "Revokes the current session if one exists. Always `204`, including when called with " +
                "no session or an already-dead one — a repeat sign-out is naturally idempotent. Still " +
                "requires a valid CSRF token.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapMe(RouteGroupBuilder group) =>
        group.MapGet("/me", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new MeQuery(), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequireAuthenticatedCaller()
            .ExemptFromPasswordChangeGate()
            .WithName("GetMe")
            .WithSummary("Report the caller's own account and session state")
            .WithDescription(
                "Replaces `GET /api/v1/reference/whoami` (removed). Always `200` once authenticated — " +
                "including while `mustChangePassword` is true, which is how the frontend learns the " +
                "flag on a hard reload rather than only right after sign-in.")
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapRefresh(RouteGroupBuilder group) =>
        group.MapPost("/refresh", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(new RefreshSessionCommand(), cancellationToken);
                return result.Match(TypedResults.Ok);
            })
            .RequireAuthenticatedCaller()
            .RequireCsrfToken()
            .WithName("RefreshSession")
            .WithSummary("Proactively extend the session's idle window")
            .WithDescription(
                "Extends the idle timeout ahead of expiry; does not rotate the session token (spec 9.1 " +
                "rotates only on privilege and password change). Call this BEFORE a session goes stale " +
                "— all three 401 variants are terminal here too, so a reactive 401 from any endpoint " +
                "should route straight to sign-in rather than calling this. Subject to the " +
                "must-change-password gate: returns `403 auth.password_change_required` while that flag " +
                "is set, unlike `me`.")
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static void MapChangePassword(RouteGroupBuilder group) =>
        group.MapPost("/password", async (
                ChangePasswordCommand command,
                HttpContext httpContext,
                ISender sender,
                CsrfTokenService csrf,
                ICurrentSession currentSession,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendAsync(command, cancellationToken);

                return result.Match(changePasswordResult =>
                {
                    AuthCookies.SetSession(httpContext, changePasswordResult.RawSessionToken);

                    // The session's IDENTITY does not change on a password change (spec 9.1 rotates only
                    // the token) — CSRF stays bound to that same session id, just reissued/rotated
                    // alongside the token per the approved delta's cookie table.
                    if (currentSession.SessionId is { } sessionId)
                    {
                        var csrfExpiresAt = timeProvider.GetUtcNow() + TimeSpan.FromHours(8);
                        var csrfToken = csrf.Issue(sessionId.ToString(), csrfExpiresAt);
                        AuthCookies.SetSessionBoundCsrf(httpContext, csrfToken);
                    }

                    return TypedResults.Ok(changePasswordResult.Session);
                });
            })
            .RequireAuthenticatedCaller()
            .RequireCsrfToken()
            .ExemptFromPasswordChangeGate()
            .RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)
            .WithName("ChangePassword")
            .WithSummary("Change the caller's own password")
            .WithDescription(
                "Requires the current password (a re-authentication check for a sensitive action, spec " +
                "9.1). Rotates the session token and revokes every OTHER active session for the account " +
                "(spec 6.1.11) — this session survives. Rejects a new password matching any of the last " +
                "five hashes. Rate-limited under the sensitive policy: `currentPassword` is an online " +
                "guessing surface too.")
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}

/// <summary>Response to <c>GET /api/v1/auth/csrf</c>.</summary>
/// <param name="CsrfToken">
/// Opaque token — also set as the `__Host-XSRF-TOKEN` cookie. Echo verbatim in an `X-CSRF-Token`
/// header on every mutating `/auth/*` request.
/// </param>
public sealed record CsrfTokenResponse(string CsrfToken);
