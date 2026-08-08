namespace SchoolManagement.Api.Security;

/// <summary>
/// Adds defensive response headers to every response.
/// </summary>
/// <remarks>
/// <para>
/// This is a JSON API, so the headers are tuned for "this response is data and must never be treated
/// as a document": a browser that can be tricked into rendering an API response as HTML turns a
/// reflected value into stored XSS.
/// </para>
/// <para>
/// Registered EARLY in the pipeline so the headers are present on error responses too — a 500 produced
/// by the exception handler needs them just as much as a 200.
/// </para>
/// </remarks>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Content-Security-Policy for API responses.
    /// </summary>
    /// <remarks>
    /// <c>default-src 'none'</c> — an API response should never cause a fetch of anything.
    /// <c>frame-ancestors 'none'</c> is the modern replacement for X-Frame-Options and blocks
    /// clickjacking; both are sent because older browsers ignore the former.
    /// </remarks>
    private const string ApiContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    /// <summary>Processes the request.</summary>
    /// <param name="context">The request context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Written on response start rather than now: an endpoint may replace the response, and headers
        // cannot be modified once the body has begun.
        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            var headers = httpContext.Response.Headers;

            // Stops a browser from second-guessing Content-Type. Without it, JSON containing HTML can be
            // sniffed as a document and executed.
            headers["X-Content-Type-Options"] = "nosniff";

            // Never leak the requested URL (which may contain identifiers) to a third-party site.
            headers["Referrer-Policy"] = "no-referrer";

            // Legacy clickjacking protection, superseded by frame-ancestors but harmless to send.
            headers["X-Frame-Options"] = "DENY";

            headers["X-Permitted-Cross-Domain-Policies"] = "none";

            // Blocks other origins from embedding these responses as a subresource.
            headers["Cross-Origin-Resource-Policy"] = "same-origin";

            // Deny powerful browser features outright; an API needs none of them.
            headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

            // Only where it will not break the interactive docs UI, which legitimately loads scripts
            // and styles. The docs endpoints are Development-only, so production always gets the
            // strict policy.
            if (!IsDocumentationPath(httpContext.Request.Path))
            {
                headers["Content-Security-Policy"] = ApiContentSecurityPolicy;
            }

            // Kestrel does not send this by default, but a reverse proxy may add one. Removing it here
            // avoids advertising the server product and version to anyone probing.
            headers.Remove("Server");

            return Task.CompletedTask;
        }, context);

        await next(context).ConfigureAwait(false);
    }

    private static bool IsDocumentationPath(PathString path) =>
        path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/docs", StringComparison.OrdinalIgnoreCase);
}
