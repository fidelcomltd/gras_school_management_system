using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Abstractions.Messaging;

/// <summary>
/// A message dispatched through the mediator pipeline.
/// </summary>
/// <typeparam name="TResponse">
/// The response. Constrained to <see cref="Result"/> so that "handlers return a Result rather
/// than throwing for expected failures" is a COMPILE-TIME guarantee rather than a convention
/// somebody has to remember. You cannot declare a request that returns a bare DTO.
/// </typeparam>
/// <remarks>
/// Deliberately invariant (no <c>out</c>). Covariance would let <c>SendAsync</c> infer
/// <typeparamref name="TResponse"/> as a base type such as <see cref="Result"/>, and the
/// mediator's cached executor lookup would then resolve the wrong closed handler type.
/// </remarks>
public interface IRequest<TResponse>
    where TResponse : Result;

/// <summary>
/// Non-generic marker for commands. Its only job is to be usable as a generic constraint, which
/// is how <c>UnitOfWorkBehavior</c> applies a transaction to commands and NOT to queries: the DI
/// container simply cannot close that behaviour over a query type. Never implement this directly.
/// </summary>
public interface IBaseCommand;

/// <summary>
/// A request that CHANGES state. Runs inside a transaction and a unit of work.
/// </summary>
/// <typeparam name="TResponse">The response, always a <see cref="Result"/>.</typeparam>
public interface ICommand<TResponse> : IRequest<TResponse>, IBaseCommand
    where TResponse : Result;

/// <summary>A state-changing request that returns no payload.</summary>
public interface ICommand : IRequest<Result>, IBaseCommand;

/// <summary>
/// A request that READS state. Must not mutate anything; gets no transaction, and repositories
/// serve it with no-tracking queries projected straight to a DTO.
/// </summary>
/// <typeparam name="TResponse">The response, always a <see cref="Result"/>.</typeparam>
public interface IQuery<TResponse> : IRequest<TResponse>
    where TResponse : Result;
