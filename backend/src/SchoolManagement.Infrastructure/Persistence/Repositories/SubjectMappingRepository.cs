using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ISubjectMappingRepository"/>.</summary>
internal sealed class SubjectMappingRepository(ApplicationDbContext context) : ISubjectMappingRepository
{
    /// <inheritdoc />
    public Task AddAsync(SubjectMapping mapping, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        cancellationToken.ThrowIfCancellationRequested();

        context.SubjectMappings.Add(mapping);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubjectMapping>> ListByTermTrackedAsync(Guid termId, CancellationToken cancellationToken) =>
        await context.SubjectMappings.Where(mapping => mapping.TermId == termId).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubjectMapping>> ListActiveByTermReadOnlyAsync(Guid termId, CancellationToken cancellationToken) =>
        await context.SubjectMappings
            .AsNoTracking()
            .Where(mapping => mapping.TermId == termId && mapping.Status == SubjectMappingStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubjectMapping>> ListActiveByLevelAndTermReadOnlyAsync(
        Guid classLevelId, Guid termId, CancellationToken cancellationToken) =>
        await context.SubjectMappings
            .AsNoTracking()
            .Where(mapping => mapping.ClassLevelId == classLevelId && mapping.TermId == termId && mapping.Status == SubjectMappingStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<bool> IsActivelyMappedAsync(Guid subjectId, Guid classLevelId, Guid termId, CancellationToken cancellationToken) =>
        context.SubjectMappings.AsNoTracking().AnyAsync(
            mapping => mapping.SubjectId == subjectId &&
                       mapping.ClassLevelId == classLevelId &&
                       mapping.TermId == termId &&
                       mapping.Status == SubjectMappingStatus.Active,
            cancellationToken);

    /// <inheritdoc />
    public Task<bool> AnyEverForSubjectAsync(Guid subjectId, CancellationToken cancellationToken) =>
        context.SubjectMappings.AsNoTracking().AnyAsync(mapping => mapping.SubjectId == subjectId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubjectMapping>> ListActiveBySubjectAndTermReadOnlyAsync(
        Guid subjectId, Guid termId, CancellationToken cancellationToken) =>
        await context.SubjectMappings
            .AsNoTracking()
            .Where(mapping => mapping.SubjectId == subjectId && mapping.TermId == termId && mapping.Status == SubjectMappingStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListSubjectIdsEverMappedToLevelAsync(Guid classLevelId, CancellationToken cancellationToken) =>
        await context.SubjectMappings
            .AsNoTracking()
            .Where(mapping => mapping.ClassLevelId == classLevelId && mapping.Status == SubjectMappingStatus.Active)
            .Select(mapping => mapping.SubjectId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
