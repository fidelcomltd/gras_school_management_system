using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Behaviors;

/// <summary>
/// PIPELINE STAGE 2. Logs the start and completion of every request, its duration, and its
/// outcome, inside a logging scope that carries the request name.
/// </summary>
/// <remarks>
/// <para>
/// WHAT IS DELIBERATELY NOT LOGGED: the request object itself. Requests carry user input, which
/// in this application will include personal data, and a log statement is the easiest way to leak
/// it into a system with a different retention policy and a wider audience. Only the type name is
/// recorded. If you need a field for diagnostics, add it explicitly to
/// <see cref="BehaviorLog"/> after confirming it is not personal data.
/// </para>
/// <para>
/// The correlation identifier is NOT threaded through here. ASP.NET Core has already started the
/// request Activity by this point, so <see cref="Activity.Current"/> carries the trace and span
/// IDs, and Serilog's enricher attaches them to every record emitted inside the request —
/// including these. That is also the identifier returned to clients as <c>traceId</c> in a
/// ProblemDetails response, so a support ticket quoting it finds these lines.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class RequestLoggingBehavior<TRequest, TResponse>(
    ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(
        TRequest request,
        PipelineContinuation<TResponse> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var requestName = typeof(TRequest).Name;
        var timestamp = Stopwatch.GetTimestamp();

        BehaviorLog.HandlingRequest(logger, requestName);

        try
        {
            var response = await continuation(cancellationToken);
            var elapsed = (long)Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

            // Guarded + hoisted to locals for the same reason as the cancellation branch below
            // (CA1873): `response.Error.Type.ToString()` is a computed argument handed to a static
            // [LoggerMessage] method, so the analyzer cannot tell it is only evaluated when Warning
            // logging is enabled unless the computation happens inside the IsEnabled guard.
            if (response.IsFailure && logger.IsEnabled(LogLevel.Warning))
            {
                var errorCode = response.Error.Code;
                var errorType = response.Error.Type.ToString();
                BehaviorLog.RequestFailed(logger, requestName, errorCode, errorType);
            }

            BehaviorLog.HandledRequest(
                logger,
                requestName,
                elapsed,
                response.IsSuccess ? "Success" : "Failure");

            return response;
        }
        catch (OperationCanceledException)
        {
            // A cancelled request is expected traffic (client navigated away, gateway timed out).
            // Logged at Information so it is visible but does not pollute the error rate. Guarded
            // so the Stopwatch computation itself is skipped when Information logging is disabled
            // (CA1873). The guard alone is not sufficient: BehaviorLog.RequestCancelled is a static
            // [LoggerMessage] method that takes `logger` as a PARAMETER, so the analyzer cannot tie
            // `logger.IsEnabled(...)` to the call the way it can for an instance method call on
            // `logger` directly. Hoisting the computation to a local INSIDE the guard — the same
            // shape the success path above already uses for `elapsed` — is what satisfies it.
            if (logger.IsEnabled(LogLevel.Information))
            {
                var elapsedMs = (long)Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
                BehaviorLog.RequestCancelled(logger, requestName, elapsedMs);
            }

            throw;
        }
    }
}
