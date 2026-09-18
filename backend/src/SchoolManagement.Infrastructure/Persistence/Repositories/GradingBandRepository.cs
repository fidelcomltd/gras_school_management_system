using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IGradingBandRepository"/>.</summary>
internal sealed class GradingBandRepository(ApplicationDbContext context) : IGradingBandRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<GradingBand>> ListReadOnlyOrderedAsync(CancellationToken cancellationToken) =>
        await context.GradingBands
            .AsNoTracking()
            .OrderBy(band => band.DisplayOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task ReplaceAllAsync(IReadOnlyList<GradingBand> bands, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bands);

        var existing = await context.GradingBands.ToListAsync(cancellationToken).ConfigureAwait(false);
        context.GradingBands.RemoveRange(existing);
        context.GradingBands.AddRange(bands);

        // No SaveChangesAsync — the unit-of-work behaviour commits the transaction.
    }
}
