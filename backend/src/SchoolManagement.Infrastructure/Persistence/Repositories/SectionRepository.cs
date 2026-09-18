using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Domain.Classes;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ISectionRepository"/>.</summary>
internal sealed class SectionRepository(ApplicationDbContext context) : ISectionRepository
{
    /// <inheritdoc />
    public Task AddAsync(Section section, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(section);
        cancellationToken.ThrowIfCancellationRequested();

        context.Sections.Add(section);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Section?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Sections.FirstOrDefaultAsync(section => section.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> NameExistsAsync(string normalizedNameKey, Guid? excludingId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedNameKey);

        var query = context.Sections.AsNoTracking().Where(section => section.NameKey == normalizedNameKey);

        if (excludingId is { } id)
        {
            query = query.Where(section => section.Id != id);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Section>> ListAllReadOnlyAsync(CancellationToken cancellationToken) =>
        await context.Sections
            .AsNoTracking()
            .OrderBy(section => section.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
