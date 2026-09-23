using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IPupilRecordRepository"/> (spec 6.5.5 to 6.5.8).</summary>
internal sealed class PupilRecordRepository(ApplicationDbContext context) : IPupilRecordRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PupilContact>> ListContactsAsync(Guid pupilId, bool track, CancellationToken cancellationToken) =>
        await Query<PupilContact>(track).Where(contact => contact.PupilId == pupilId).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuthorisedPickupPerson>> ListPickupPersonsAsync(Guid pupilId, bool track, CancellationToken cancellationToken) =>
        await Query<AuthorisedPickupPerson>(track).Where(person => person.PupilId == pupilId).OrderBy(person => person.DisplayOrder)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<BarredPersonAnswer?> FindBarredAnswerAsync(Guid pupilId, bool track, CancellationToken cancellationToken) =>
        Query<BarredPersonAnswer>(track).FirstOrDefaultAsync(answer => answer.PupilId == pupilId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<BarredPerson>> ListBarredPersonsAsync(Guid pupilId, bool track, CancellationToken cancellationToken) =>
        await Query<BarredPerson>(track).Where(person => person.PupilId == pupilId).OrderBy(person => person.DisplayOrder)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<PupilHealth?> FindHealthAsync(Guid pupilId, bool track, CancellationToken cancellationToken) =>
        Query<PupilHealth>(track).FirstOrDefaultAsync(health => health.PupilId == pupilId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PupilDocument>> ListDocumentsAsync(Guid pupilId, bool track, CancellationToken cancellationToken) =>
        await Query<PupilDocument>(track).Where(document => document.PupilId == pupilId).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task AddAsync(object entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();
        context.Add(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(object entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();
        context.Remove(entity);
        return Task.CompletedTask;
    }

    private IQueryable<T> Query<T>(bool track)
        where T : class => track ? context.Set<T>() : context.Set<T>().AsNoTracking();
}
