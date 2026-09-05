using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Domain.Auth;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IAdminAccountRepository"/>.</summary>
internal sealed class AdminAccountRepository(
    ApplicationDbContext context,
    DbContextOptions<ApplicationDbContext> options)
    : IAdminAccountRepository
{
    /// <inheritdoc />
    public Task<bool> AnyExistsAsync(CancellationToken cancellationToken) =>
        context.AdminAccounts.AsNoTracking().AnyAsync(cancellationToken);

    /// <inheritdoc />
    public Task AddAsync(AdminAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        cancellationToken.ThrowIfCancellationRequested();

        context.AdminAccounts.Add(account);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AdminAccount?> FindTrackedByEmailAsync(string email, CancellationToken cancellationToken) =>
        context.AdminAccounts.FirstOrDefaultAsync(account => account.Email == email, cancellationToken);

    /// <inheritdoc />
    public Task<AdminAccount?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.AdminAccounts.FirstOrDefaultAsync(account => account.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<AdminAccount?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.AdminAccounts.AsNoTracking().FirstOrDefaultAsync(account => account.Id == id, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Deliberately opens its OWN <see cref="ApplicationDbContext"/> over the same
    /// <see cref="DbContextOptions{TContext}"/> rather than reusing the scoped <c>context</c>
    /// field — see the interface member's remarks for why this write must be isolated from whatever
    /// transaction the ambient unit of work has open on the scoped context.
    /// </remarks>
    public async Task PersistLockoutStateAsync(AdminAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        await using var isolatedContext = new ApplicationDbContext(options);

        await isolatedContext.AdminAccounts
            .Where(row => row.Id == account.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.FailedLoginCount, account.FailedLoginCount)
                    .SetProperty(row => row.LastFailedLoginAtUtc, account.LastFailedLoginAtUtc)
                    .SetProperty(row => row.LockedUntilUtc, account.LockedUntilUtc),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
