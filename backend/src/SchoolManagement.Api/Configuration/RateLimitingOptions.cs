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
