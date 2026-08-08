using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Abstractions.Messaging;

/// <summary>
/// Handles exactly one <typeparamref name="TRequest"/>. Exactly one handler per request:
/// registration is scanned, and two handlers for the same request is a startup failure.
/// </summary>
/// <typeparam name="TRequest">The request type handled.</typeparam>
/// <typeparam name="TResponse">The response, always a <see cref="Result"/>.</typeparam>
/// <remarks>
/// Handlers are <c>internal sealed</c> by convention (enforced by <c>HandlerConventionTests</c>):
/// they are an implementation detail reached only through <see cref="ISender"/>, never called
/// directly by an endpoint or by another handler.
/// </remarks>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    /// <summary>Executes the request.</summary>
    /// <param name="request">The request. Already validated by the pipeline.</param>
    /// <param name="cancellationToken">
    /// Must be passed to every downstream async call. A handler that drops the token turns a
    /// cancelled HTTP request into wasted database work; CA2016 enforces forwarding it.
    /// </param>
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
