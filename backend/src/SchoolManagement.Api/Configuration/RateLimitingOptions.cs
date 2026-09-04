using Microsoft.Extensions.Options;

namespace SchoolManagement.Api.Configuration;

/// <summary>
/// Rate-limiting configuration, bound from the <c>RateLimiting</c> section.
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// The policy applied to every endpoint unless it opts into another.
    /// </summary>
    public const string DefaultPolicyName = "default";

    /// <summary>
    /// A deliberately tight policy for expensive or abuse-prone endpoints — login, password reset,
    /// anything that sends a message or costs money. Apply it with
    /// <c>.RequireRateLimiting(RateLimitingOptions.SensitivePolicyName)</c>.
    /// </summary>
    public const string SensitivePolicyName = "sensitive";

    /// <summary>Requests permitted per window under the default policy. Default 100.</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>Length of the default policy's window, in seconds. Default 60.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// How many requests may wait for a permit instead of being rejected immediately. Default 0.
    /// </summary>
    /// <remarks>
    /// Zero on purpose. Queueing makes a client wait rather than telling it to back off, so under load
    /// the queue fills with requests whose callers have already timed out — work nobody is waiting for,
    /// crowding out work somebody is. A prompt 429 with <c>Retry-After</c> is more useful than a slow
    /// success.
    /// </remarks>
    public int QueueLimit { get; set; }

    /// <summary>Requests permitted per window under the sensitive policy. Default 10.</summary>
    public int SensitivePermitLimit { get; set; } = 10;

    /// <summary>Length of the sensitive policy's window, in seconds. Default 60.</summary>
    public int SensitiveWindowSeconds { get; set; } = 60;

    /// <summary>
    /// The policy applied to <c>/health/ready</c>. Deliberately separate from
    /// <see cref="DefaultPolicyName"/> — see <see cref="HealthPolicyName"/>'s remarks.
    /// </summary>
    public const string HealthPolicyName = "health";

    /// <summary>
    /// Requests permitted per window under the health policy. Default 120 (2/second sustained).
    /// </summary>
    /// <remarks>
    /// Sized for a probe, not a client: it must never reject a legitimate readiness check, because a
    /// rejected probe reads as "unhealthy" and can trigger a fleet restart (see the remarks on
    /// <c>HealthEndpoints</c>). The assumed floor is a 5-second probe interval — the tightest of the
    /// common defaults (Kubernetes' own default is 10s, its allowed minimum is 1s; AWS ALB/NLB's
    /// minimum is 5s; Docker's default is 30s) — with up to 3 independent probers sharing one
    /// partition key (a load balancer, an orchestrator's kubelet, and an external uptime monitor can
    /// all appear to originate from the same address behind a NAT or shared egress). That worst case
    /// is 3 × (60/5) = 36 requests/minute; 120 leaves more than 3x headroom above it while still
    /// capping an anonymous, database-touching endpoint far below "unlimited".
    /// </remarks>
    public int HealthPermitLimit { get; set; } = 120;

    /// <summary>Length of the health policy's window, in seconds. Default 60.</summary>
    public int HealthWindowSeconds { get; set; } = 60;
}

/// <summary>Validates <see cref="RateLimitingOptions"/> at startup.</summary>
internal sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        Check(nameof(RateLimitingOptions.PermitLimit), options.PermitLimit, 1, 1_000_000);
        Check(nameof(RateLimitingOptions.WindowSeconds), options.WindowSeconds, 1, 3_600);
        Check(nameof(RateLimitingOptions.SensitivePermitLimit), options.SensitivePermitLimit, 1, 100_000);
        Check(nameof(RateLimitingOptions.SensitiveWindowSeconds), options.SensitiveWindowSeconds, 1, 3_600);
        Check(nameof(RateLimitingOptions.HealthPermitLimit), options.HealthPermitLimit, 1, 1_000_000);
        Check(nameof(RateLimitingOptions.HealthWindowSeconds), options.HealthWindowSeconds, 1, 3_600);

        if (options.QueueLimit is < 0 or > 10_000)
        {
            failures.Add(
                $"'{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.QueueLimit)}' must " +
                $"be between 0 and 10000, but was {options.QueueLimit}.");
        }

        if (options.SensitivePermitLimit > options.PermitLimit)
        {
            failures.Add(
                $"'{RateLimitingOptions.SectionName}:" +
                $"{nameof(RateLimitingOptions.SensitivePermitLimit)}' " +
                $"({options.SensitivePermitLimit}) is higher than " +
                $"'{nameof(RateLimitingOptions.PermitLimit)}' ({options.PermitLimit}). The sensitive " +
                "policy is meant to be stricter than the default; this configuration makes it looser, " +
                "which is almost certainly a mistake.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;

        void Check(string property, int value, int minimum, int maximum)
        {
            if (value < minimum || value > maximum)
            {
                failures.Add(
                    $"'{RateLimitingOptions.SectionName}:{property}' must be between {minimum} and " +
                    $"{maximum}, but was {value}.");
            }
        }
    }
}
