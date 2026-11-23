using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IPupilRegNumberHistoryRepository"/>.</summary>
internal sealed class PupilRegNumberHistoryRepository(ApplicationDbContext context) : IPupilRegNumberHistoryRepository
{
    /// <inheritdoc />
    public Task AddAsync(PupilRegNumberHistory row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        cancellationToken.ThrowIfCancellationRequested();

        context.PupilRegNumberHistory.Add(row);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string registrationNumber, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationNumber);

        return context.PupilRegNumberHistory
            .AsNoTracking()
            .AnyAsync(row => row.OldRegistrationNumber == registrationNumber, cancellationToken);
    }
}
