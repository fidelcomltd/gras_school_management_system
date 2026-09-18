using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Domain.Classes;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IClassLevelRepository"/>.</summary>
internal sealed class ClassLevelRepository(ApplicationDbContext context) : IClassLevelRepository
{
    /// <inheritdoc />
    public Task AddAsync(ClassLevel level, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(level);
        cancellationToken.ThrowIfCancellationRequested();

        context.ClassLevels.Add(level);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ClassLevel?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.ClassLevels.FirstOrDefaultAsync(level => level.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task RemoveAsync(ClassLevel level, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(level);
        cancellationToken.ThrowIfCancellationRequested();

        context.ClassLevels.Remove(level);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> NameExistsAsync(string normalizedNameKey, Guid? excludingId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedNameKey);

        var query = context.ClassLevels.AsNoTracking().Where(level => level.NameKey == normalizedNameKey);

        if (excludingId is { } id)
        {
            query = query.Where(level => level.Id != id);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClassLevel>> ListAllTrackedAsync(CancellationToken cancellationToken) =>
        await context.ClassLevels.ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClassLevel>> ListAllReadOnlyAsync(CancellationToken cancellationToken) =>
        await context.ClassLevels.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<ClassLevel?> FindReferencingNextLevelAsync(Guid id, CancellationToken cancellationToken) =>
        context.ClassLevels.AsNoTracking().FirstOrDefaultAsync(level => level.NextLevelId == id, cancellationToken);

    /// <inheritdoc />
    /// <remarks>See the interface member's remarks for why this raw, immediately-executed statement exists at all.</remarks>
    public async Task NegateProgressionOrdersAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return;
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE class_levels SET progression_order = -progression_order WHERE id = ANY({ids.ToArray()})",
            cancellationToken).ConfigureAwait(false);
    }
}
