namespace SchoolManagement.Api.Security;

/// <summary>
/// The exact cookie attribute set for the session and CSRF cookies (approved contract delta §6).
/// One place, so the two cookies stay a coherent set end to end (CLAUDE.md §5) rather than each call
/// site picking its own options.
/// </summary>
/// <remarks>
/// <para>
/// <c>__Host-</c> PREFIX: both cookies are host-locked (no <c>Domain</c> attribute, <c>Path=/</c>,
/// <c>Secure</c>) rather than domain-scoped. This is what closes the one gap a double-submit CSRF
/// cookie otherwise has — a sibling subdomain that could plant its own cookie on a shared registrable
/// domain cannot touch a <c>__Host-</c> cookie at all.
/// </para>
/// <para>
/// <c>SameSite=Lax</c> ASSUMES the deployed frontend and API share a registrable domain (approved
/// delta §6, tracked as <c>## Known drift</c> against the deployment decision, Open question 5). A
/// cross-site deployment needs <c>SameSite=None</c> and a different CSRF posture — not a one-line
/// change here.
/// </para>
/// </remarks>
internal static class AuthCookies
{
    /// <summary>The session cookie's name. Matches the <c>CookieSession</c> security scheme's declared cookie name.</summary>
    public const string SessionCookieName = "__Host-Session";

    /// <summary>The CSRF cookie's name. Read by axios's <c>xsrfCookieName</c> on the frontend (TASK-0021).</summary>
    public const string CsrfCookieName = "__Host-XSRF-TOKEN";

    /// <summary>Spec 6.1.11's 8-hour absolute cap, mirrored as the session cookie's browser-side lifetime.</summary>
    private static readonly TimeSpan SessionCookieMaxAge = TimeSpan.FromHours(8);

    /// <summary>Pre-auth CSRF cookie lifetime (approved delta §6): anonymous, from <c>GET /auth/csrf</c>.</summary>
    private static readonly TimeSpan AnonymousCsrfCookieMaxAge = TimeSpan.FromHours(1);

    /// <summary>Post-auth CSRF cookie lifetime, bound to the session and rotated alongside it.</summary>
    private static readonly TimeSpan SessionBoundCsrfCookieMaxAge = TimeSpan.FromHours(8);

    /// <summary>Sets the <c>__Host-Session</c> cookie to <paramref name="rawSessionToken"/>.</summary>
    public static void SetSession(HttpContext httpContext, string rawSessionToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.Cookies.Append(SessionCookieName, rawSessionToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = SessionCookieMaxAge,
        });
    }

    /// <summary>Clears the session cookie (sign-out) — server-side revocation happens separately.</summary>
    public static void ClearSession(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.Cookies.Delete(SessionCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
        });
    }

    /// <summary>Sets the <c>__Host-XSRF-TOKEN</c> cookie, pre-auth (anonymous) lifetime.</summary>
    public static void SetAnonymousCsrf(HttpContext httpContext, string csrfToken) =>
        SetCsrf(httpContext, csrfToken, AnonymousCsrfCookieMaxAge);

    /// <summary>Sets the <c>__Host-XSRF-TOKEN</c> cookie, bound-to-session lifetime.</summary>
    public static void SetSessionBoundCsrf(HttpContext httpContext, string csrfToken) =>
        SetCsrf(httpContext, csrfToken, SessionBoundCsrfCookieMaxAge);

    /// <summary>Clears the CSRF cookie (sign-out).</summary>
    public static void ClearCsrf(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.Cookies.Delete(CsrfCookieName, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
        });
    }

    private static void SetCsrf(HttpContext httpContext, string csrfToken, TimeSpan maxAge)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.Cookies.Append(CsrfCookieName, csrfToken, new CookieOptions
        {
            // NOT HttpOnly — axios's xsrfCookieName reads it directly from JS (approved delta §5).
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = maxAge,
        });
    }
}
