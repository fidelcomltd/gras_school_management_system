using Microsoft.Extensions.Options;

namespace SchoolManagement.Api.Observability;

/// <summary>
/// Telemetry configuration, bound from the <c>Observability</c> section.
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Observability";

    /// <summary>
    /// The service name reported to the telemetry backend. Must be stable across deployments — it is
    /// the key everything is grouped by, so changing it splits a service's history in two.
    /// </summary>
    public string ServiceName { get; set; } = "school-management-api";

    /// <summary>
    /// OTLP collector endpoint, for example <c>http://localhost:4317</c>. Leave EMPTY to disable
    /// export and log to the console only.
    /// </summary>
    /// <remarks>
    /// Empty is the correct default for local development and for the test suite: exporting to a
    /// collector that is not running produces a steady stream of connection errors that drown the logs
    /// a developer is actually reading.
    /// </remarks>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Fraction of traces sampled, from 0.0 to 1.0. Default 1.0 (all).
    /// </summary>
    /// <remarks>
    /// Sample everything until traffic makes it expensive. Lower it in production if volume demands,
    /// but be aware that head sampling discards traces before knowing whether they contained an error.
    /// </remarks>
    public double TraceSampleRatio { get; set; } = 1.0;

    /// <summary>Whether an OTLP endpoint is configured.</summary>
    public bool IsOtlpExportEnabled => !string.IsNullOrWhiteSpace(OtlpEndpoint);
}

/// <summary>Validates <see cref="ObservabilityOptions"/> at startup.</summary>
internal sealed class ObservabilityOptionsValidator : IValidateOptions<ObservabilityOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ObservabilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            failures.Add(
                $"'{ObservabilityOptions.SectionName}:{nameof(ObservabilityOptions.ServiceName)}' is " +
                "required.");
        }

        if (options.TraceSampleRatio is < 0.0 or > 1.0)
        {
            failures.Add(
                $"'{ObservabilityOptions.SectionName}:" +
                $"{nameof(ObservabilityOptions.TraceSampleRatio)}' must be between 0.0 and 1.0, but " +
                $"was {options.TraceSampleRatio}.");
        }

        if (options.IsOtlpExportEnabled &&
            !Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out _))
        {
            failures.Add(
                $"'{ObservabilityOptions.SectionName}:{nameof(ObservabilityOptions.OtlpEndpoint)}' " +
                $"must be an absolute URL such as 'http://localhost:4317', but was " +
                $"'{options.OtlpEndpoint}'. Leave it empty to disable telemetry export.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
