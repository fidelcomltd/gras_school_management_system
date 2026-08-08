using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Behaviors;

/// <summary>
/// PIPELINE STAGE 4. Warns when a request exceeds the configured slow-request threshold.
/// </summary>
/// <remarks>
/// <para>
/// This is a cheap, always-on tripwire, not a substitute for tracing: OpenTelemetry gives you the
/// span breakdown, but only if you already know to go looking. A warning in the log is what makes
/// you go looking.
/// </para>
/// <para>
/// Positioned INSIDE validation and OUTSIDE the transaction, so the measurement covers the handler
/// and its database work — the parts that actually get slow — without counting validation, which
/// is in-memory and constant-time.
/// </para>
/// <para>
/// <see cref="IOptionsMonitor{TOptions}"/> rather than <see cref="IOptions{TOptions}"/> so the
/// threshold can be retuned in configuration without a restart, which is the whole point of a
/// diagnostic knob.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class PerformanceBehavior<TRequest, TResponse>(
    IOptionsMonitor<PipelineOptions> options,
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
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

        var timestamp = Stopwatch.GetTimestamp();

        var response = await continuation(cancellationToken);

        var elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
        var threshold = options.CurrentValue.SlowRequestThresholdMilliseconds;

        if (elapsedMilliseconds > threshold)
        {
            BehaviorLog.SlowRequest(logger, typeof(TRequest).Name, elapsedMilliseconds, threshold);
        }

        return response;
    }
}
