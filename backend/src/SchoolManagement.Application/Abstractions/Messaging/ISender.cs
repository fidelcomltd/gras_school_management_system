using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Abstractions.Messaging;

/// <summary>
/// Dispatches a request through the pipeline to its handler. This is the ONLY way an endpoint
/// reaches application logic — endpoints never resolve or call a handler directly, because that
/// would skip validation, logging, timing, and the transaction.
/// </summary>
public interface ISender
{
    /// <summary>
    /// Sends <paramref name="request"/> through the behaviour pipeline to its handler.
    /// </summary>
    /// <typeparam name="TResponse">The response, inferred from the request.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">
    /// The caller's token. In an endpoint this is the <c>HttpContext.RequestAborted</c> token
    /// that Minimal APIs binds automatically when you declare a <see cref="CancellationToken"/>
    /// parameter.
    /// </param>
    /// <returns>The handler's result, after every behaviour has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// No handler is registered for the request type. This is a wiring defect and surfaces at
    /// the first dispatch; <c>PipelineTests</c> assert every request in the assembly resolves.
    /// </exception>
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        where TResponse : Result;
}
