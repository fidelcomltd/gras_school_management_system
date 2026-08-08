using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace SchoolManagement.Api.Observability;

/// <summary>
/// Adds the current trace and span IDs to every log record.
/// </summary>
/// <remarks>
/// This is what makes a log line and a distributed trace findable from each other, and what makes the
/// <c>traceId</c> returned in an error response worth quoting in a support ticket. Written by hand
/// rather than taking another package: it is six lines and reads directly off
/// <see cref="Activity.Current"/>, which ASP.NET Core has already populated.
/// </remarks>
internal sealed class TraceContextEnricher : ILogEventEnricher
{
    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        var activity = Activity.Current;

        if (activity is null)
        {
            return;
        }

        logEvent.AddOrUpdateProperty(
            propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));

        logEvent.AddOrUpdateProperty(
            propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
    }
}

/// <summary>
/// Replaces the value of any log property whose NAME suggests it holds a credential or personal data.
/// </summary>
/// <remarks>
/// <para>
/// A BACKSTOP, NOT A LICENCE. The primary rule stands: do not log sensitive values in the first place —
/// which is why the pipeline logs request TYPE names and never request contents. This exists because
/// logging statements are added under pressure by many hands, and one careless
/// <c>logger.LogInformation("Payload {@Request}", request)</c> can put credentials into a log store
/// with a long retention period and a wide audience. Redaction at the sink means that mistake is
/// contained rather than permanent.
/// </para>
/// <para>
/// It matches on name SUBSTRINGS, case-insensitively, so <c>UserPassword</c> and <c>access_token</c>
/// are both caught. It cannot catch a sensitive value stored under an innocuous name — no
/// name-based filter can — so review still matters.
/// </para>
/// </remarks>
internal sealed class RedactSensitivePropertiesEnricher : ILogEventEnricher
{
    /// <summary>The placeholder written in place of a redacted value.</summary>
    public const string RedactedPlaceholder = "[redacted]";

    /// <summary>
    /// Property-name fragments that trigger redaction.
    /// </summary>
    /// <remarks>
    /// Add to this list rather than removing from it. Each entry is here because that name routinely
    /// carries either a credential or personal data.
    /// </remarks>
    private static readonly string[] SensitiveNameFragments =
    [
        "password", "passwd", "pwd",
        "secret", "token", "apikey", "api_key",
        "authorization", "auth_header", "cookie",
        "connectionstring", "connection_string",
        "creditcard", "credit_card", "cardnumber", "cvv",
        "ssn", "socialsecurity", "nationalid", "national_id",
        "privatekey", "private_key",
    ];

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        // Materialised first: redacting calls AddOrUpdateProperty, which mutates the collection being
        // enumerated.
        var propertyNames = logEvent.Properties.Keys.ToArray();

        foreach (var propertyName in propertyNames)
        {
            if (IsSensitive(propertyName))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(
                    propertyName,
                    new ScalarValue(RedactedPlaceholder)));
            }
        }
    }

    private static bool IsSensitive(string propertyName)
    {
        foreach (var fragment in SensitiveNameFragments)
        {
            if (propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
