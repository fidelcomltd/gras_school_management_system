using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Abstractions.Persistence;

/// <summary>
/// Transactional boundary for commands. Implemented over EF Core in Infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This abstraction exists so Application never names an EF Core type — enforced by
/// <c>DependencyDirectionTests.ApplicationDoesNotReferenceEntityFrameworkCore</c>.
/// </para>
/// <para>
/// WHY THE WHOLE OPERATION IS PASSED IN, rather than exposing Begin/Commit/Rollback: the
/// PostgreSQL connection is configured with <c>EnableRetryOnFailure</c>, and EF Core refuses a
/// user-initiated transaction under a retrying execution strategy unless the entire unit is
/// wrapped in that strategy so it can be replayed. Handing over the delegate is what makes
/// "retry on transient failure" and "one transaction per command" coexist. Splitting this into
/// Begin/Commit across await boundaries would throw at runtime the first time a retry fired.
/// </para>
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// True when a transaction is already open on this scope. Used by the unit-of-work behaviour
    /// to avoid attempting a nested transaction, which PostgreSQL/EF Core do not support here.
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>Persists tracked changes. Prefer letting the unit-of-work behaviour call this.</summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="operation"/> inside one transaction, retried as a whole on transient
    /// database failures.
    /// </summary>
    /// <remarks>
    /// Commits when the operation returns a successful <see cref="Result"/>; rolls back on a
    /// failed <see cref="Result"/> or an exception. This is why the return type is constrained to
    /// <see cref="Result"/> — a failed command must not half-commit just because it declined to
    /// throw.
    /// </remarks>
    /// <typeparam name="TResult">The operation's result type.</typeparam>
    /// <param name="operation">
    /// The work to perform. Must be safe to execute more than once: a retry re-runs it from the
    /// start. Do not capture mutable state from outside, and do not perform non-database side
    /// effects (emails, HTTP calls, queue publishes) inside it.
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<TResult> ExecuteAtomicallyAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
        where TResult : Result;
}
