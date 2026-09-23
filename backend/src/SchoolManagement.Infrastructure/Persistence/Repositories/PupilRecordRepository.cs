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
    public async Task<PupilRecordSet> LoadForPupilsAsync(IReadOnlyCollection<Guid> pupilIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pupilIds);
        var ids = pupilIds.ToArray();

        var contacts = await Query<PupilContact>(track: false).Where(row => ids.Contains(row.PupilId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var barred = await Query<BarredPersonAnswer>(track: false).Where(row => ids.Contains(row.PupilId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var health = await Query<PupilHealth>(track: false).Where(row => ids.Contains(row.PupilId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var pickup = await Query<AuthorisedPickupPerson>(track: false).Where(row => ids.Contains(row.PupilId)).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var documents = await Query<PupilDocument>(track: false).Where(row => ids.Contains(row.PupilId)).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var admissions = await context.AdmissionRecords.AsNoTracking().Where(row => ids.Contains(row.PupilId)).ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PupilRecordSet(
            contacts.ToLookup(row => row.PupilId),
            barred.ToDictionary(row => row.PupilId),
            health.ToDictionary(row => row.PupilId),
            pickup.ToLookup(row => row.PupilId),
            documents.ToLookup(row => row.PupilId),
            admissions.ToDictionary(row => row.PupilId));
    }

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
