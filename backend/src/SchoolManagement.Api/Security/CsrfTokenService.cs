using System.Globalization;
using Microsoft.AspNetCore.DataProtection;

namespace SchoolManagement.Api.Security;

/// <summary>
/// Mints and validates the double-submit CSRF token (approved contract delta §5): "a literal
/// double-submit cookie... Validation: stateless — an HMAC over a rotating server-side key, the
/// session id... and an expiry."
/// </summary>
/// <remarks>
/// <para>
/// IMPLEMENTATION CHOICE: ASP.NET Core's Data Protection API (<see cref="IDataProtector"/>) stands in
/// for the hand-rolled "HMAC over a rotating server-side key" the delta describes, rather than
/// building one from <see cref="Application.Abstractions.Secrets.ISecretProvider"/> plus
/// <c>System.Security.Cryptography.HMACSHA256</c> directly. Data Protection already IS an
/// authenticated (tamper-evident), automatically KEY-ROTATING mechanism — exactly the two properties
/// the delta asks for — and needs no configuration entry of its own, so there is nothing new for a
/// deployment to provision or a test fixture to stub. The token this produces is opaque, changes
/// completely if tampered with, and cannot be forged without the server's key ring — which is what
/// makes the equality check below more than a naive double-submit (see the class this replaces
/// nothing of: the cookie must ALSO round-trip through <see cref="Validate"/>, not merely equal the
/// header).
/// </para>
/// <para>
/// TOKEN SHAPE: <c>{subject}|{expiryUnixSeconds}</c>, protected. <c>subject</c> is
/// <see cref="AnonymousSubject"/> pre-auth (issued by <c>GET /auth/csrf</c>, consumed by
/// <c>POST /auth/sign-in</c>, which has no session yet) or the session id once one exists.
/// </para>
/// </remarks>
internal sealed class CsrfTokenService(IDataProtectionProvider dataProtectionProvider)
{
    /// <summary>Subject recorded in a CSRF token minted before any session exists.</summary>
    public const string AnonymousSubject = "anon";

    private const string Purpose = "SchoolManagement.Auth.Csrf.v1";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(Purpose);

    /// <summary>Mints a token bound to <paramref name="subject"/>, valid until <paramref name="expiresAt"/>.</summary>
    public string Issue(string subject, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var payload = string.Join(
            '|',
            subject,
            expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

        return _protector.Protect(payload);
    }

    /// <summary>
    /// Validates that <paramref name="cookieValue"/> and <paramref name="headerValue"/> are both
    /// present, equal (the double-submit half), and that the value is a token this service actually
    /// minted for <paramref name="expectedSubject"/> and not yet expired (the signed-token half).
    /// </summary>
    public bool Validate(string? cookieValue, string? headerValue, string expectedSubject, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSubject);

        return TryValidateSignedToken(cookieValue, headerValue, now, out var subject) &&
            string.Equals(subject, expectedSubject, StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates <paramref name="cookieValue"/>/<paramref name="headerValue"/> exactly as
    /// <see cref="Validate"/> does — double-submit equality, genuinely minted by this service, not
    /// expired — but WITHOUT checking which subject the token names.
    /// </summary>
    /// <remarks>
    /// Second-pass review MEDIUM 4: sign-out is deliberately tolerant of an already-dead session
    /// (approved delta §2 — "204 either way... rather than getting a 401 loop"). The session cookie
    /// lives 8h while the idle deadline is 30 min, so a sign-out issued after the idle deadline arrives
    /// with a CSRF cookie bound to that now-expired SESSION, while the caller is (correctly) no longer
    /// authenticated — the strict check would compare that session-bound subject against
    /// <see cref="AnonymousSubject"/> and reject a legitimate sign-out with <c>csrf.invalid</c>. The
    /// security property this preserves is unaffected: sign-out is idempotent and its worst outcome is
    /// an unwanted logout, not a privileged action, so accepting any validly-signed, unexpired token —
    /// regardless of which session it names — is a deliberate, narrow relaxation for this one endpoint.
    /// </remarks>
    public bool ValidateIgnoringSubject(string? cookieValue, string? headerValue, DateTimeOffset now) =>
        TryValidateSignedToken(cookieValue, headerValue, now, out _);

    private bool TryValidateSignedToken(
        string? cookieValue,
        string? headerValue,
        DateTimeOffset now,
        out string subject)
    {
        subject = string.Empty;

        if (string.IsNullOrEmpty(cookieValue) || string.IsNullOrEmpty(headerValue))
        {
            return false;
        }

        // Double-submit: the header must literally echo the cookie — this is what a cross-site
        // attacker cannot arrange unless they can also plant the cookie (closed by __Host-, not by
        // this check).
        if (!string.Equals(cookieValue, headerValue, StringComparison.Ordinal))
        {
            return false;
        }

        string payload;

        try
        {
            payload = _protector.Unprotect(cookieValue);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }

        var parts = payload.Split('|');

        if (parts.Length != 2 ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expiryUnixSeconds))
        {
            return false;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiryUnixSeconds);

        if (now >= expiresAt)
        {
            return false;
        }

        subject = parts[0];
        return true;
    }
}
