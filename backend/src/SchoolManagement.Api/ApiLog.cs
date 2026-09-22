namespace SchoolManagement.Api;

/// <summary>
/// Source-generated log messages for the Api layer.
/// </summary>
/// <remarks>
/// Same rules as <c>BehaviorLog</c>: CA1848 is an error, so all logging goes through
/// <c>[LoggerMessage]</c>, and no parameter may carry personal data. Request PATHS are logged;
/// query strings are not, because they frequently contain identifiers and search terms.
/// </remarks>
internal static partial class ApiLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Error,
        Message = "Unhandled exception for request path {RequestPath}")]
    public static partial void UnhandledException(ILogger logger, string requestPath, Exception exception);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Database constraint violation translated to error code {ErrorCode}")]
    public static partial void TranslatedPersistenceFailure(
        ILogger logger,
        string errorCode,
        Exception exception);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Request to {RequestPath} was aborted by the client")]
    public static partial void RequestAborted(ILogger logger, string requestPath);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "Rejected a malformed request to {RequestPath} with status {StatusCode}")]
    public static partial void RejectedMalformedRequest(
        ILogger logger,
        string requestPath,
        int statusCode);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Rejected a malformed inbound {HeaderName} header and generated a replacement")]
    public static partial void RejectedMalformedCorrelationHeader(ILogger logger, string headerName);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "Registered {ModuleCount} endpoint module(s): {ModuleNames}")]
    public static partial void RegisteredEndpointModules(
        ILogger logger,
        int moduleCount,
        string moduleNames);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Warning,
        Message = "DataProtection:KeyRingPath is not set in environment {EnvironmentName}. The key ring " +
                  "may not survive a restart, and outstanding CSRF tokens would then need re-fetching " +
                  "from GET /auth/csrf. Set it to a persisted directory on this host.")]
    public static partial void DataProtectionKeyRingNotPersisted(ILogger logger, string environmentName);
}
