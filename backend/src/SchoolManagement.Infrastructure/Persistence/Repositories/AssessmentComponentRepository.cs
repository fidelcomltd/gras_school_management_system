using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IAssessmentComponentRepository"/>.</summary>
internal sealed class AssessmentComponentRepository(ApplicationDbContext context) : IAssessmentComponentRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AssessmentComponent>> ListReadOnlyOrderedAsync(CancellationToken cancellationToken) =>
        await context.AssessmentComponents
            .AsNoTracking()
            .OrderBy(component => component.DisplayOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssessmentComponent>> ListTrackedAsync(CancellationToken cancellationToken) =>
        await context.AssessmentComponents
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task AddAsync(AssessmentComponent component, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(component);
        cancellationToken.ThrowIfCancellationRequested();

        context.AssessmentComponents.Add(component);

        // No SaveChangesAsync — the unit-of-work behaviour commits the transaction.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(AssessmentComponent component, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(component);
        cancellationToken.ThrowIfCancellationRequested();

        context.AssessmentComponents.Remove(component);

        // No SaveChangesAsync — the unit-of-work behaviour commits the transaction.
        return Task.CompletedTask;
    }
}
