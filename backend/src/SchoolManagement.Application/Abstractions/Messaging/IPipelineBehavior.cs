using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Abstractions.Messaging;

/// <summary>
/// Invokes the next stage of the pipeline, ending at the handler.
/// </summary>
/// <typeparam name="TResponse">The response, always a <see cref="Result"/>.</typeparam>
/// <param name="cancellationToken">
/// Taken as a parameter rather than captured so a behaviour can substitute a linked or
/// timeout-bounded token for the stages beneath it.
/// </param>
public delegate Task<TResponse> PipelineContinuation<TResponse>(CancellationToken cancellationToken)
    where TResponse : Result;

/// <summary>
/// A cross-cutting stage wrapped around every request: logging, validation, timing, transactions.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response, always a <see cref="Result"/>.</typeparam>
/// <remarks>
/// <para>
/// ORDER IS SIGNIFICANT AND IS SET BY REGISTRATION ORDER in
/// <see cref="ApplicationDependencyInjection.AddApplication"/>. The first registered behaviour is
/// the OUTERMOST — it sees the request first and the response last. Read that method before
/// adding one; inserting a behaviour in the wrong position is a silent behaviour change.
/// </para>
/// <para>
/// A behaviour that needs to run only for commands should constrain
/// <typeparamref name="TRequest"/> to <see cref="IBaseCommand"/> rather than test the type at
/// runtime: the container then cannot even construct it for a query.
/// </para>
/// </remarks>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    /// <summary>Runs this stage, calling <paramref name="continuation"/> to proceed.</summary>
    /// <param name="request">The request travelling down the pipeline.</param>
    /// <param name="continuation">
    /// The next stage. Call it exactly once for pass-through behaviour; do NOT call it to
    /// short-circuit (as validation does when the request is invalid).
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<TResponse> HandleAsync(
        TRequest request,
        PipelineContinuation<TResponse> continuation,
        CancellationToken cancellationToken);
}
