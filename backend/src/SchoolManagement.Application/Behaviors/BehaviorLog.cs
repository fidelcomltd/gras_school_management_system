using Microsoft.Extensions.Logging;

namespace SchoolManagement.Application.Behaviors;

/// <summary>
/// Source-generated log messages for the pipeline behaviours.
/// </summary>
/// <remarks>
/// <para>
/// All logging in this codebase goes through <c>[LoggerMessage]</c> partial methods. CA1848 is an
/// ERROR, so <c>logger.LogInformation($"...")</c> will not compile. Two reasons: the generated
/// code allocates nothing and skips formatting entirely when the level is disabled, and — more
/// importantly — it forces every value into a NAMED structured field instead of being flattened
/// into a message string, which is what makes the logs queryable.
/// </para>
/// <para>
/// PII RULE: never add a parameter here that could carry personal data. Log identifiers, type
/// names, counts and durations. Request contents are not logged; see
/// <c>RequestLoggingBehavior</c>.
/// </para>
/// <para>
/// These live in a separate non-generic class because the logging source generator does not
/// emit for methods declared inside an open generic type, which every behaviour is.
/// </para>
/// </remarks>
internal static partial class BehaviorLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Handling {RequestName}")]
    public static partial void HandlingRequest(ILogger logger, string requestName);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Handled {RequestName} in {ElapsedMilliseconds}ms with outcome {Outcome}")]
    public static partial void HandledRequest(
        ILogger logger,
        string requestName,
        long elapsedMilliseconds,
        string outcome);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "{RequestName} failed with error code {ErrorCode} of type {ErrorType}")]
    public static partial void RequestFailed(
        ILogger logger,
        string requestName,
        string errorCode,
        string errorType);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing {RequestName}")]
    public static partial void UnhandledException(ILogger logger, string requestName, Exception exception);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "{RequestName} was cancelled by the caller after {ElapsedMilliseconds}ms")]
    public static partial void RequestCancelled(
        ILogger logger,
        string requestName,
        long elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Warning,
        Message = "{RequestName} is slow: took {ElapsedMilliseconds}ms, threshold is {ThresholdMilliseconds}ms")]
    public static partial void SlowRequest(
        ILogger logger,
        string requestName,
        long elapsedMilliseconds,
        int thresholdMilliseconds);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Debug,
        Message = "{RequestName} failed validation on {InvalidPropertyCount} property/properties")]
    public static partial void ValidationFailed(
        ILogger logger,
        string requestName,
        int invalidPropertyCount);
}
