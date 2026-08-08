using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Persistence;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>.
/// </summary>
internal sealed class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    /// <inheritdoc />
    public bool HasActiveTransaction => context.Database.CurrentTransaction is not null;

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<TResult> ExecuteAtomicallyAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
        where TResult : Result
    {
        ArgumentNullException.ThrowIfNull(operation);

        // The connection is configured with EnableRetryOnFailure, and EF Core refuses a
        // user-initiated transaction under a retrying execution strategy unless the whole unit is
        // inside strategy.ExecuteAsync — otherwise a retry would resume mid-transaction on a
        // connection whose transaction no longer exists. This is why IUnitOfWork takes the operation
        // instead of exposing Begin/Commit.
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(token);

            var result = await operation(token);

            if (result.IsSuccess)
            {
                await context.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
            }
            else
            {
                // A failed Result rolls back. This is what makes "a command that returns a failure
                // never half-commits" structural rather than something each handler must remember.
                await transaction.RollbackAsync(token);
            }

            return result;
        }, cancellationToken);

        // If the operation throws, DisposeAsync on the transaction rolls it back, and the exception
        // continues up to the global exception handler, which turns it into a 500 with a traceId.
    }
}
