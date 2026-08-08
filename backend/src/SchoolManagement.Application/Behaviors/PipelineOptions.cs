using Microsoft.Extensions.Options;

namespace SchoolManagement.Application.Behaviors;

/// <summary>
/// Tuning for the mediator pipeline. Bound from the <c>Pipeline</c> configuration section.
/// </summary>
public sealed class PipelineOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Pipeline";

    /// <summary>
    /// A request slower than this logs a warning. Default 500ms.
    /// </summary>
    /// <remarks>
    /// This is a smoke alarm, not a performance budget: it should be loose enough that a warning
    /// means "look at this" rather than becoming background noise everyone filters out.
    /// </remarks>
    public int SlowRequestThresholdMilliseconds { get; set; } = 500;
}

/// <summary>
/// Validates <see cref="PipelineOptions"/> at startup.
/// </summary>
/// <remarks>
/// Hand-written <see cref="IValidateOptions{TOptions}"/> rather than data annotations: it needs no
/// extra package in this layer, it produces a message that names the configuration key a developer
/// has to go and fix, and it is directly unit-testable. Wired with <c>ValidateOnStart()</c> in the
/// Api composition root, so a bad value stops the process at boot instead of surfacing on the
/// first request that happens to be slow.
/// </remarks>
internal sealed class PipelineOptionsValidator : IValidateOptions<PipelineOptions>
{
    private const int MinimumThresholdMilliseconds = 1;
    private const int MaximumThresholdMilliseconds = 60_000;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, PipelineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.SlowRequestThresholdMilliseconds is < MinimumThresholdMilliseconds
            or > MaximumThresholdMilliseconds)
        {
            return ValidateOptionsResult.Fail(
                $"Configuration '{PipelineOptions.SectionName}:" +
                $"{nameof(PipelineOptions.SlowRequestThresholdMilliseconds)}' must be between " +
                $"{MinimumThresholdMilliseconds} and {MaximumThresholdMilliseconds}, but was " +
                $"{options.SlowRequestThresholdMilliseconds}.");
        }

        return ValidateOptionsResult.Success;
    }
}
