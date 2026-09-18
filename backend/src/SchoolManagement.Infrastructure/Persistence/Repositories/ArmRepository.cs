using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Domain.Classes;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IArmRepository"/>.</summary>
internal sealed class ArmRepository(ApplicationDbContext context) : IArmRepository
{
    /// <inheritdoc />
    public Task AddAsync(Arm arm, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arm);
        cancellationToken.ThrowIfCancellationRequested();

        context.Arms.Add(arm);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Arm?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Arms.FirstOrDefaultAsync(arm => arm.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<Arm?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Arms.AsNoTracking().FirstOrDefaultAsync(arm => arm.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task RemoveAsync(Arm arm, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arm);
        cancellationToken.ThrowIfCancellationRequested();

        context.Arms.Remove(arm);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Arm>> ListAllReadOnlyAsync(CancellationToken cancellationToken) =>
        await context.Arms.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Arm>> ListBySessionTrackedAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await context.Arms.Where(arm => arm.SessionId == sessionId).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<bool> LabelExistsAsync(
        Guid classLevelId,
        Guid sessionId,
        string normalizedLabelKey,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedLabelKey);

        var query = context.Arms.AsNoTracking().Where(arm =>
            arm.ClassLevelId == classLevelId && arm.SessionId == sessionId && arm.LabelKey == normalizedLabelKey);

        if (excludingId is { } id)
        {
            query = query.Where(arm => arm.Id != id);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> AnyForSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        context.Arms.AsNoTracking().AnyAsync(arm => arm.SessionId == sessionId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> AnyForLevelAsync(Guid classLevelId, CancellationToken cancellationToken) =>
        context.Arms.AsNoTracking().AnyAsync(arm => arm.ClassLevelId == classLevelId, cancellationToken);

    /// <inheritdoc />
    public Task<int> CountBySessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        context.Arms.AsNoTracking().CountAsync(arm => arm.SessionId == sessionId, cancellationToken);
}
