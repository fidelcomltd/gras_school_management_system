using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SchoolManagement.Api.Configuration;
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
/// <para>
/// Anonymous plus a real database round trip makes <c>/health/ready</c> a cheap amplifier against the
/// database unless it is throttled — but throttling a probe at the SAME rate as a normal endpoint
/// risks rejecting a legitimate check, which reads as "unhealthy" and can trigger a fleet restart.
/// It runs under its own dedicated, deliberately generous policy
/// (<see cref="RateLimitingOptions.HealthPolicyName"/>) instead of either extreme.
/// <c>/health/live</c> needs no such policy: it runs no dependency checks, so it costs nothing to
/// answer and amplifies nothing.
/// </para>
/// <para>
/// <c>/health/live</c> also reports the git commit the process was deployed from (<c>commit</c>, or
/// <c>unknown</c>), so a deploy can be confirmed from outside. A commit SHA names code, not
/// infrastructure, and the repository is the only place it leads; it is the one deliberate exception
/// to "no detail".
/// </para>
/// </remarks>
public static class HealthEndpoints
{
    /// <summary>Liveness probe path.</summary>
    public const string LivePath = "/health/live";

    /// <summary>Readiness probe path.</summary>
    public const string ReadyPath = "/health/ready";

    /// <summary>Configuration key Render sets on every deploy to the commit it built.</summary>
    public const string DeployedCommitKey = "RENDER_GIT_COMMIT";

    /// <summary>Reported when the host names no commit (local runs, the VPS).</summary>
    public const string UnknownCommit = "unknown";

    /// <summary>Maps the health endpoints.</summary>
    /// <param name="app">The application to map onto.</param>
    public static void MapHealthEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var commit = DeployedCommit(app.Configuration);

        app.MapHealthChecks(LivePath, new HealthCheckOptions
        {
            // Excludes every registered check: liveness is about this process only.
            Predicate = static _ => false,
            ResponseWriter = (context, report) => WriteMinimalResponseAsync(context, report, commit),
        })
        .AllowAnonymous()
        .WithTags("Health")
        .ExcludeFromDescription();

        app.MapHealthChecks(ReadyPath, new HealthCheckOptions
        {
            Predicate = static registration =>
                registration.Tags.Contains(InfrastructureDependencyInjection.ReadinessTag),
            ResponseWriter = static (context, report) => WriteMinimalResponseAsync(context, report, commit: null),
        })
        .AllowAnonymous()
        .WithTags("Health")
        .ExcludeFromDescription()
        // Anonymous AND a real database round trip — see the class remarks on the tension this
        // resolves. RateLimitingOptions.HealthPermitLimit documents the numbers and the probe
        // interval they assume. /health/live is not throttled: it runs no dependency checks, so it
        // is not an amplifier against anything.
        .RequireRateLimiting(RateLimitingOptions.HealthPolicyName);
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
    private static Task WriteMinimalResponseAsync(HttpContext context, HealthReport report, string? commit)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
        });
        object payload = commit is null
            ? new { status = report.Status.ToString(), checks }
            : new { status = report.Status.ToString(), commit, checks };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(payload, HealthJsonOptions),
            context.RequestAborted);
    }

    /// <summary>
    /// The commit this process was built from, so a deploy can be confirmed from outside by comparing it
    /// with the pushed branch head. Only a full git SHA (40 hex characters, or 64 for SHA-256) is echoed,
    /// lower-cased so it matches <c>git rev-parse</c>: this endpoint is anonymous, so a value that is
    /// anything else (a misconfigured variable holding a secret, say) is never reflected back.
    /// </summary>
    internal static string DeployedCommit(IConfiguration configuration)
    {
        var value = configuration[DeployedCommitKey]?.Trim().ToLowerInvariant();
        return value is { Length: 40 or 64 } && value.All(char.IsAsciiHexDigitLower)
            ? value
            : UnknownCommit;
    }

    /// <summary>Web defaults, so the probe payload is camelCase like the rest of the API.</summary>
    private static readonly JsonSerializerOptions HealthJsonOptions = new(JsonSerializerDefaults.Web);
}
