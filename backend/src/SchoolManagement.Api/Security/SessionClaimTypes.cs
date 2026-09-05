namespace SchoolManagement.Api.Security;

/// <summary>
/// Claim type names <see cref="CookieSessionAuthenticationHandler"/> attaches to the authenticated
/// principal, beyond the standard <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>
/// (the account id, read by the existing <c>HttpCurrentUser</c>).
/// </summary>
internal static class SessionClaimTypes
{
    /// <summary>The current <c>AdminSession</c> row's id. Read by <see cref="HttpCurrentSession"/>.</summary>
    public const string SessionId = "sid";

    /// <summary>
    /// <c>"true"</c>/<c>"false"</c> — whether the must-change-password gate currently applies. Read
    /// by <see cref="MustChangePasswordGateMiddleware"/>. Reflects the account's state AT THE MOMENT
    /// this request authenticated, not a cached value from sign-in — the authentication handler
    /// re-derives it from the database on every request.
    /// </summary>
    public const string MustChangePassword = "mcp";
}
