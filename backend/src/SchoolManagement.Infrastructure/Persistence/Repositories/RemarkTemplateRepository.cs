using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IRemarkTemplateRepository"/> (TASK-0086 stage B).</summary>
internal sealed class RemarkTemplateRepository(ApplicationDbContext context) : IRemarkTemplateRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<RemarkTemplate>> ListByKindReadOnlyAsync(RemarkKind kind, CancellationToken cancellationToken) =>
        await context.RemarkTemplates
            .AsNoTracking()
            .Where(template => template.Kind == kind)
            .OrderBy(template => template.CreatedAtUtc)
            .ThenBy(template => template.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<bool> ExistsWithTextAsync(RemarkKind kind, string normalizedTextKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedTextKey);

        return await context.RemarkTemplates
            .AsNoTracking()
            .AnyAsync(template => template.Kind == kind && template.TextKey == normalizedTextKey, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<RemarkTemplate?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await context.RemarkTemplates
            .FirstOrDefaultAsync(template => template.Id == id, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task AddAsync(RemarkTemplate remarkTemplate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remarkTemplate);
        cancellationToken.ThrowIfCancellationRequested();

        context.RemarkTemplates.Add(remarkTemplate);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(RemarkTemplate remarkTemplate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remarkTemplate);
        cancellationToken.ThrowIfCancellationRequested();

        context.RemarkTemplates.Remove(remarkTemplate);

        return Task.CompletedTask;
    }
}
