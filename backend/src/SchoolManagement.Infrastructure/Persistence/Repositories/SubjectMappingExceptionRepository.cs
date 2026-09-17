using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ISubjectMappingExceptionRepository"/>.</summary>
internal sealed class SubjectMappingExceptionRepository(ApplicationDbContext context) : ISubjectMappingExceptionRepository
{
    /// <inheritdoc />
    public Task AddAsync(SubjectMappingException exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);
        cancellationToken.ThrowIfCancellationRequested();

        context.SubjectMappingExceptions.Add(exception);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SubjectMappingException?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.SubjectMappingExceptions.FirstOrDefaultAsync(exception => exception.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task RemoveAsync(SubjectMappingException exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);
        cancellationToken.ThrowIfCancellationRequested();

        context.SubjectMappingExceptions.Remove(exception);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubjectMappingException>> ListByArmAndTermReadOnlyAsync(
        Guid armId, Guid termId, CancellationToken cancellationToken) =>
        await context.SubjectMappingExceptions
            .AsNoTracking()
            .Where(exception => exception.ArmId == armId && exception.TermId == termId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubjectMappingException>> ListByTermReadOnlyAsync(Guid termId, CancellationToken cancellationToken) =>
        await context.SubjectMappingExceptions
            .AsNoTracking()
            .Where(exception => exception.TermId == termId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid armId, Guid subjectId, Guid termId, CancellationToken cancellationToken) =>
        context.SubjectMappingExceptions.AsNoTracking().AnyAsync(
            exception => exception.ArmId == armId && exception.SubjectId == subjectId && exception.TermId == termId,
            cancellationToken);
}
