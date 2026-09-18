using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ISubjectRepository"/>.</summary>
internal sealed class SubjectRepository(ApplicationDbContext context) : ISubjectRepository
{
    /// <inheritdoc />
    public Task AddAsync(Subject subject, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);
        cancellationToken.ThrowIfCancellationRequested();

        context.Subjects.Add(subject);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Subject?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Subjects.FirstOrDefaultAsync(subject => subject.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<Subject?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Subjects.AsNoTracking().FirstOrDefaultAsync(subject => subject.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task RemoveAsync(Subject subject, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);
        cancellationToken.ThrowIfCancellationRequested();

        context.Subjects.Remove(subject);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Subject>> ListAllReadOnlyAsync(CancellationToken cancellationToken) =>
        await context.Subjects.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<bool> NameExistsAsync(string normalizedNameKey, Guid? excludingId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedNameKey);

        var query = context.Subjects.AsNoTracking().Where(subject => subject.NameKey == normalizedNameKey);

        if (excludingId is { } id)
        {
            query = query.Where(subject => subject.Id != id);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Subject?> FindByCodeKeyAsync(string normalizedCodeKey, Guid? excludingId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedCodeKey);

        var query = context.Subjects.AsNoTracking().Where(subject => subject.CodeKey == normalizedCodeKey);

        if (excludingId is { } id)
        {
            query = query.Where(subject => subject.Id != id);
        }

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}
