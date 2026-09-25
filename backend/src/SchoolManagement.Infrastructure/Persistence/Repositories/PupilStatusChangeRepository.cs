using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IPupilStatusChangeRepository"/>.</summary>
internal sealed class PupilStatusChangeRepository(ApplicationDbContext context) : IPupilStatusChangeRepository
{
    /// <inheritdoc />
    public Task AddAsync(PupilStatusChange row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        cancellationToken.ThrowIfCancellationRequested();

        context.PupilStatusChanges.Add(row);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PupilStatusChange>> ListByPupilReadOnlyAsync(Guid pupilId, CancellationToken cancellationToken) =>
        await context.PupilStatusChanges.AsNoTracking()
            .Where(row => row.PupilId == pupilId)
            .OrderBy(row => row.ChangedAtUtc)
            .ThenBy(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
