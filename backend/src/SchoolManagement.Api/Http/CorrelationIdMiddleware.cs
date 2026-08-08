using System.Diagnostics;

namespace SchoolManagement.Api.Http;

/// <summary>
/// Accepts a caller-supplied correlation ID, attaches it to the trace, and echoes it back.
/// </summary>
/// <remarks>
/// <para>
/// Lets a request be followed across the frontend, this service and anything downstream. The value is
/// attached as a tag on the current <see cref="Activity"/>, so OpenTelemetry exports it and Serilog
/// records it on every log line inside the request.
/// </para>
/// <para>
/// SECURITY — the inbound value is NEVER echoed unvalidated. It is written into a response header and
/// into logs, so an attacker-controlled string is a header-injection and log-forging vector: a newline
/// would let them inject additional response headers or fabricate log entries. Anything that is not a
/// short, plain token is discarded and replaced with a generated one.
/// </para>
/// </remarks>
internal sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    /// <summary>The request and response header carrying the correlation ID.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Tag name used on the trace span and on log records.</summary>
    public const string ActivityTagName = "correlation_id";

    /// <summary>
    /// Longest accepted inbound value. Generous enough for a GUID or a trace ID, short enough that it
    /// cannot be used to stuff the logs.
    /// </summary>
    private const int MaxLength = 128;

    /// <summary>Processes the request.</summary>
    /// <param name="context">The request context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = ResolveCorrelationId(context);

        context.Items[HeaderName] = correlationId;
        Activity.Current?.SetTag(ActivityTagName, correlationId);

        // Set on the response before the body starts: headers cannot be added once the response has
        // begun, and an endpoint that streams would otherwise lose the header.
        context.Response.OnStarting(static state =>
        {
            var (httpContext, id) = ((HttpContext, string))state;
            httpContext.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        }, (context, correlationId));

        await next(context).ConfigureAwait(false);
    }

    private string ResolveCorrelationId(HttpContext context)
    {
        var inbound = context.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(inbound))
        {
            return GenerateCorrelationId(context);
        }

        if (IsSafe(inbound))
        {
            return inbound;
        }

        ApiLog.RejectedMalformedCorrelationHeader(logger, HeaderName);
        return GenerateCorrelationId(context);
    }

    /// <summary>
    /// Prefers the current trace ID so the correlation ID and the distributed trace agree, which is
    /// what makes a support ticket quoting one findable by the other.
    /// </summary>
    private static string GenerateCorrelationId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

    /// <summary>
    /// Allows only unreserved token characters. Deliberately an ALLOW-list: a deny-list of dangerous
    /// characters is one forgotten code point away from being bypassed.
    /// </summary>
    private static bool IsSafe(string value)
    {
        if (value.Length > MaxLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            var isAllowed = char.IsAsciiLetterOrDigit(character)
                || character is '-' or '_' or '.' or ':';

            if (!isAllowed)
            {
                return false;
            }
        }

        return true;
    }
}
