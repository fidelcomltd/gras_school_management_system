using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SchoolManagement.Infrastructure;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Liveness and readiness endpoints.
/// </summary>
/// <remarks>
/// <para>
/// THE DISTINCTION MATTERS, and getting it wrong causes outages:
/// </para>
/// <list type="bullet">
/// <item><c>/health/live</c> — "is this process functioning?" Runs NO dependency checks. If it fails,
/// the orchestrator should restart the container.</item>
/// <item><c>/health/ready</c> — "can this instance serve traffic?" Includes the database. If it fails,
/// the orchestrator should stop sending traffic but NOT restart.</item>
/// </list>
/// <para>
/// If liveness also checked the database, a brief database outage would fail liveness on every
/// instance, so the orchestrator would restart the entire fleet — turning a recoverable dependency
/// blip into a full cold start, with a thundering herd hitting the database as it recovers.
/// </para>
/// <para>
/// These are NOT mapped as part of the versioned API group. A probe URL is infrastructure, not
/// contract: it is configured in a deployment manifest, and moving it to <c>/api/v2/health</c> would
/// break every manifest for no benefit. They are also anonymous by necessity — a probe has no
/// credentials — and deliberately expose no diagnostic detail for that reason.
/// </para>
/// </remarks>
public static class HealthEndpoints
{
    /// <summary>Liveness probe path.</summary>
    public const string LivePath = "/health/live";

    /// <summary>Readiness probe path.</summary>
    public const string ReadyPath = "/health/ready";

    /// <summary>Maps the health endpoints.</summary>
    /// <param name="app">The application to map onto.</param>
    public static void MapHealthEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks(LivePath, new HealthCheckOptions
        {
            // Excludes every registered check: liveness is about this process only.
            Predicate = static _ => false,
            ResponseWriter = WriteMinimalResponseAsync,
        })
        .AllowAnonymous()
        .WithTags("Health")
        .ExcludeFromDescription();

        app.MapHealthChecks(ReadyPath, new HealthCheckOptions
        {
            Predicate = static registration =>
                registration.Tags.Contains(InfrastructureDependencyInjection.ReadinessTag),
            ResponseWriter = WriteMinimalResponseAsync,
        })
        .AllowAnonymous()
        .WithTags("Health")
        .ExcludeFromDescription();
    }

    /// <summary>
    /// Writes only the aggregate status and each check's name and status.
    /// </summary>
    /// <remarks>
    /// The default writer would return exception messages and durations. These endpoints are anonymous,
    /// so that would hand an unauthenticated caller connection strings, server names and library
    /// versions from failed-check exception text. Operators get the detail from logs and traces, which
    /// are correlated by trace ID; the probe gets the status code it actually acts on.
    /// </remarks>
    private static Task WriteMinimalResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
            }),
        };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(payload, HealthJsonOptions),
            context.RequestAborted);
    }

    /// <summary>Web defaults, so the probe payload is camelCase like the rest of the API.</summary>
    private static readonly JsonSerializerOptions HealthJsonOptions = new(JsonSerializerDefaults.Web);
}
