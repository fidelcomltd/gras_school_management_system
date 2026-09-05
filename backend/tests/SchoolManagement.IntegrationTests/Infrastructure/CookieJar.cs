namespace SchoolManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Manual cookie tracking for the auth integration tests, instead of relying on
/// <c>HttpClient</c>'s built-in <c>CookieContainer</c> handling.
/// </summary>
/// <remarks>
/// The built-in handling only sends a <c>Secure</c> cookie back when the client's <c>BaseAddress</c>
/// scheme is <c>https</c>, and — more importantly — never exposes a cookie's VALUE back out, which
/// these tests need in order to echo the CSRF cookie into an <c>X-CSRF-Token</c> header. Reading
/// <c>Set-Cookie</c> response headers directly and replaying a hand-built <c>Cookie</c> request header
/// sidesteps both problems and makes exactly what each request carries visible in the test itself.
/// </remarks>
internal sealed class CookieJar
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <summary>The current <c>__Host-XSRF-TOKEN</c> value, or <c>null</c> if never set.</summary>
    public string? CsrfToken => _values.GetValueOrDefault("__Host-XSRF-TOKEN");

    /// <summary>Whether a <c>__Host-Session</c> cookie is currently held.</summary>
    public bool HasSession => _values.ContainsKey("__Host-Session");

    /// <summary>Captures every <c>Set-Cookie</c> header on <paramref name="response"/>.</summary>
    public void Capture(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            return;
        }

        foreach (var setCookie in setCookies)
        {
            var firstPair = setCookie.Split(';', 2)[0];
            var separatorIndex = firstPair.IndexOf('=', StringComparison.Ordinal);

            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = firstPair[..separatorIndex];
            var value = firstPair[(separatorIndex + 1)..];

            // Max-Age=0 (or an empty value) is how AuthCookies.ClearSession/ClearCsrf delete a cookie.
            if (value.Length == 0 || setCookie.Contains("Max-Age=0", StringComparison.OrdinalIgnoreCase))
            {
                _values.Remove(name);
            }
            else
            {
                _values[name] = value;
            }
        }
    }

    /// <summary>Attaches every currently-held cookie to <paramref name="request"/>.</summary>
    public void Apply(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_values.Count == 0)
        {
            return;
        }

        request.Headers.TryAddWithoutValidation(
            "Cookie",
            string.Join("; ", _values.Select(pair => $"{pair.Key}={pair.Value}")));
    }

    /// <summary>Attaches every cookie AND the CSRF header (for a mutating request).</summary>
    public void ApplyWithCsrf(HttpRequestMessage request)
    {
        Apply(request);

        if (CsrfToken is { } token)
        {
            request.Headers.TryAddWithoutValidation("X-CSRF-Token", token);
        }
    }
}
