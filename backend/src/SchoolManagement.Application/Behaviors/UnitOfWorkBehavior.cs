using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Persistence;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Behaviors;

/// <summary>
/// PIPELINE STAGE 5 (innermost). Wraps COMMANDS ONLY in a transaction and saves on success.
/// </summary>
/// <remarks>
/// <para>
/// HOW "COMMANDS ONLY" IS ENFORCED: <typeparamref name="TRequest"/> is constrained to
/// <see cref="IBaseCommand"/>. The behaviour is registered as an open generic, and the DI container
/// will not close a generic type whose constraints a candidate does not satisfy — so for a query
/// this behaviour is not merely skipped at runtime, it is never constructed. There is no
/// <c>if (request is ICommand)</c> to get wrong, and a query physically cannot acquire a
/// transaction.
/// </para>
/// <para>
/// Innermost so the transaction is held for the shortest possible time: validation, logging and
/// timing have all completed before a database transaction is opened. Holding a transaction open
/// across validation would mean a slow validator holds row locks.
/// </para>
/// <para>
/// A handler therefore does NOT call <c>SaveChangesAsync</c>. It mutates the domain and returns a
/// Result; this stage commits on success and rolls back on failure. That is what makes "a command
/// that returns a failure leaves no partial write behind" true by construction rather than by
/// each handler remembering.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The command type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IBaseCommand
    where TResponse : Result
{
    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(
        TRequest request,
        PipelineContinuation<TResponse> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        // A command dispatched from inside another command already has a transaction. Opening a
        // second would fail: EF Core does not support nested transactions on one connection.
        if (unitOfWork.HasActiveTransaction)
        {
            return await continuation(cancellationToken);
        }

        // Wrapped in a lambda rather than passed directly: PipelineContinuation<T> and
        // Func<CancellationToken, Task<T>> have identical shapes but are distinct delegate types,
        // so there is no implicit conversion between them.
        return await unitOfWork.ExecuteAtomicallyAsync(
            token => continuation(token),
            cancellationToken);
    }
}
