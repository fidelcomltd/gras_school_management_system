using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Messaging;

/// <summary>
/// The mediator. Resolves the handler for a request, wraps it in the registered behaviours, and
/// invokes the chain.
/// </summary>
/// <remarks>
/// <para>
/// Hand-rolled rather than MediatR — see <c>docs/adr/0002-mediator.md</c>. Summary: MediatR 13+
/// is no longer Apache-2.0, and this whole file is the part of MediatR we actually used.
/// </para>
/// <para>
/// The one subtlety: <see cref="ISender.SendAsync"/> receives the request as
/// <c>IRequest&lt;TResponse&gt;</c>, so the concrete request type — needed to resolve
/// <c>IRequestHandler&lt;TRequest, TResponse&gt;</c> — is only known at runtime. We close a
/// generic executor over the concrete type once and cache it per request type, so the reflection
/// cost is paid once per request type for the lifetime of the process, not per dispatch.
/// </para>
/// <para>
/// NOTE FOR NATIVE AOT: the <see cref="Activator.CreateInstance(Type)"/> call below is not
/// trim-safe. If this service ever needs to be published AOT, replace the cache with a source
/// generator that emits the closed executors at compile time. Not a concern for a container
/// deployment, which is the documented target.
/// </para>
/// </remarks>
internal sealed class Sender(IServiceProvider serviceProvider) : ISender
{
    // Keyed by concrete request type. A request type determines its response type (IRequest<T>
    // is invariant and a request implements it once), so the key needs no response component.
    private static readonly ConcurrentDictionary<Type, object> ExecutorCache = new();

    /// <inheritdoc />
    public Task<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken)
        where TResponse : Result
    {
        ArgumentNullException.ThrowIfNull(request);

        var executor = (RequestExecutor<TResponse>)ExecutorCache.GetOrAdd(
            request.GetType(),
            static requestType => CreateExecutor<TResponse>(requestType));

        return executor.ExecuteAsync(request, serviceProvider, cancellationToken);
    }

    private static object CreateExecutor<TResponse>(Type requestType)
        where TResponse : Result
    {
        var executorType = typeof(RequestExecutor<,>).MakeGenericType(requestType, typeof(TResponse));

        return Activator.CreateInstance(executorType)
            ?? throw new InvalidOperationException(
                $"Could not create a pipeline executor for request type '{requestType}'.");
    }

    /// <summary>
    /// Non-generic-over-request view of an executor, so instances can live in one cache.
    /// </summary>
    private abstract class RequestExecutor<TResponse>
        where TResponse : Result
    {
        public abstract Task<TResponse> ExecuteAsync(
            IRequest<TResponse> request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken);
    }

    private sealed class RequestExecutor<TRequest, TResponse> : RequestExecutor<TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : Result
    {
        public override Task<TResponse> ExecuteAsync(
            IRequest<TResponse> request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            var typedRequest = (TRequest)request;

            var handler = serviceProvider.GetService<IRequestHandler<TRequest, TResponse>>()
                ?? throw new InvalidOperationException(
                    $"No handler is registered for request '{typeof(TRequest)}'. Expected an " +
                    $"implementation of IRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}> " +
                    "in the Application assembly. Handlers are discovered by assembly scanning in " +
                    "AddApplication(); check the handler is not nested inside another type.");

            PipelineContinuation<TResponse> pipeline =
                token => handler.HandleAsync(typedRequest, token);

            // Registration order is outermost-first, so compose from the end backwards: the
            // last behaviour registered ends up nearest the handler.
            var behaviors = serviceProvider
                .GetServices<IPipelineBehavior<TRequest, TResponse>>()
                .ToArray();

            for (var i = behaviors.Length - 1; i >= 0; i--)
            {
                var behavior = behaviors[i];
                var next = pipeline;
                pipeline = token => behavior.HandleAsync(typedRequest, next, token);
            }

            return pipeline(cancellationToken);
        }
    }
}
