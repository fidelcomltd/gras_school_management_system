using Microsoft.Extensions.Options;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Database configuration, bound from the <c>Database</c> section.
/// </summary>
/// <remarks>
/// <see cref="ConnectionString"/> IS A SECRET. It is never committed: locally it comes from
/// <c>dotnet user-secrets</c>, and in a deployment from an environment variable or the secret store.
/// <c>appsettings.json</c> carries the non-secret knobs only. See README "Configuration and secrets".
/// </remarks>
public sealed class DatabaseOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Database";

    /// <summary>
    /// Npgsql connection string. SECRET — supply via user-secrets or the environment, never in a
    /// committed file.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// How many times a transient failure is retried before giving up. Default 3.
    /// </summary>
    /// <remarks>
    /// Applies to transient faults only (connection drops, timeouts) — never to a constraint
    /// violation, which would fail identically every time.
    /// </remarks>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>Longest gap between retries, in seconds. Default 10.</summary>
    public int MaxRetryDelaySeconds { get; set; } = 10;

    /// <summary>
    /// Per-command timeout in seconds. Default 30.
    /// </summary>
    /// <remarks>
    /// A bound is the point: without one, a pathological query holds a connection until the pool is
    /// exhausted and the whole service stops responding, rather than one request failing.
    /// </remarks>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether EF Core includes parameter VALUES in logs and exception messages.
    /// </summary>
    /// <remarks>
    /// DEVELOPMENT ONLY. Parameter values are user data — enabling this in a deployed environment
    /// writes personal data into logs. Startup REFUSES to boot with this enabled outside
    /// Development; see <c>InfrastructureDependencyInjection.AddInfrastructure</c>.
    /// </remarks>
    public bool EnableSensitiveDataLogging { get; set; }

    /// <summary>
    /// Whether EF Core adds per-property detail to exceptions. Costs performance; development default.
    /// </summary>
    public bool EnableDetailedErrors { get; set; }
}

/// <summary>Validates <see cref="DatabaseOptions"/> at startup.</summary>
/// <remarks>
/// Wired with <c>ValidateOnStart()</c>, so a missing connection string stops the process at boot with
/// a message naming the key — rather than surfacing as a confusing failure on the first request that
/// happens to touch the database.
/// </remarks>
internal sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add(
                $"'{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.ConnectionString)}' is " +
                "required. For local development set it with: dotnet user-secrets set " +
                $"\"{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.ConnectionString)}\" " +
                "\"<your connection string>\" --project src/SchoolManagement.Api");
        }

        if (options.MaxRetryCount is < 0 or > 20)
        {
            failures.Add(
                $"'{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.MaxRetryCount)}' must be " +
                $"between 0 and 20, but was {options.MaxRetryCount}.");
        }

        if (options.MaxRetryDelaySeconds is < 1 or > 300)
        {
            failures.Add(
                $"'{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.MaxRetryDelaySeconds)}' " +
                $"must be between 1 and 300, but was {options.MaxRetryDelaySeconds}.");
        }

        if (options.CommandTimeoutSeconds is < 1 or > 600)
        {
            failures.Add(
                $"'{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.CommandTimeoutSeconds)}' " +
                $"must be between 1 and 600, but was {options.CommandTimeoutSeconds}.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
