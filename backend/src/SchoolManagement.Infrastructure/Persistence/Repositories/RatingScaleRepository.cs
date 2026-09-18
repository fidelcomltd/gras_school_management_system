using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRatingScaleRepository"/>. Queries <c>rating_scale</c> and
/// <c>rating_scale_point</c> as two flat, independently-queried tables and groups them in memory —
/// see <see cref="RatingScale"/>'s remarks for why there is no EF navigation to join instead.
/// </summary>
internal sealed class RatingScaleRepository(ApplicationDbContext context) : IRatingScaleRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<RatingScale>> ListReadOnlyOrderedAsync(CancellationToken cancellationToken)
    {
        var scales = await context.RatingScales
            .AsNoTracking()
            .OrderBy(scale => scale.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var points = await context.RatingScalePoints
            .AsNoTracking()
            .OrderBy(point => point.PointOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var pointsByScale = points.ToLookup(point => point.RatingScaleId);

        return scales
            .Select(scale => RatingScale.Create(scale.Id, scale.Name, pointsByScale[scale.Id].ToList()))
            .ToList();
    }

    /// <inheritdoc />
    public async Task ReplaceAllAsync(IReadOnlyList<RatingScale> scales, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scales);

        var existingPoints = await context.RatingScalePoints.ToListAsync(cancellationToken).ConfigureAwait(false);
        context.RatingScalePoints.RemoveRange(existingPoints);

        var existingScales = await context.RatingScales.ToListAsync(cancellationToken).ConfigureAwait(false);
        context.RatingScales.RemoveRange(existingScales);

        context.RatingScales.AddRange(scales);
        context.RatingScalePoints.AddRange(scales.SelectMany(scale => scale.Points));

        // No SaveChangesAsync — the unit-of-work behaviour commits the transaction.
    }
}
