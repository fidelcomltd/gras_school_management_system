using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Behaviors;

/// <summary>
/// PIPELINE STAGE 1 (outermost). Logs any exception escaping the pipeline, then rethrows.
/// </summary>
/// <remarks>
/// <para>
/// It does NOT convert the exception into a failed <see cref="Result"/>. An exception here means
/// an unexpected defect, and the caller must get a 500 with a <c>traceId</c> — which is the
/// global exception handler's job (<c>GlobalExceptionHandler</c> in the Api project). Swallowing
/// it into a Result would let a genuine bug masquerade as a handled business outcome.
/// </para>
/// <para>
/// Outermost so it sees exceptions thrown by every other behaviour, not just by the handler.
/// The correlation/trace identifier is already on the log record: ASP.NET Core starts the
/// request <see cref="System.Diagnostics.Activity"/> before the mediator is reached, so the
/// trace ID is in scope here without this behaviour having to plumb it through.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class UnhandledExceptionBehavior<TRequest, TResponse>(
    ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)
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

        try
        {
            return await continuation(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Caught broadly and rethrown unchanged: this stage observes, it does not handle.
            // OperationCanceledException is excluded because a client disconnecting is normal
            // traffic, not an error — the logging behaviour records it at Information.
            BehaviorLog.UnhandledException(logger, typeof(TRequest).Name, exception);
            throw;
        }
    }
}
