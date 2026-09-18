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
    /// <remarks>
    /// TASK-0072 STAGE 1 REVIEW FIX: this used to blindly delete every row and reinsert the submitted
    /// set, minting a fresh id for every scale and point on EVERY save — harmless for
    /// <see cref="GradingBand"/>, which nothing references, but wrong for a rating scale, which stage
    /// 2/3 rating blocks reference BY ID. This now DIFFS against what is currently persisted: a scale
    /// or point whose id matches an existing row is UPDATED IN PLACE (id preserved, via
    /// <see cref="RatingScale.Rename"/>/<see cref="RatingScalePoint.Update"/> on the TRACKED entity,
    /// so EF issues an <c>UPDATE</c>, never a <c>DELETE</c>+<c>INSERT</c> of the same id); an existing
    /// row whose id is absent from <paramref name="scales"/> (a scale) or its owning scale's submitted
    /// points (a point) is removed; anything with no matching id is a genuinely new row. The caller
    /// (<c>UpdateRatingScalesCommandHandler</c>) has already resolved which submitted id is which —
    /// this method trusts <paramref name="scales"/>' ids exactly as given.
    /// </remarks>
    public async Task ReplaceAllAsync(IReadOnlyList<RatingScale> scales, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scales);

        var existingScalesById = await context.RatingScales
            .ToDictionaryAsync(scale => scale.Id, cancellationToken)
            .ConfigureAwait(false);

        var existingPointsByScale = (await context.RatingScalePoints
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToLookup(point => point.RatingScaleId);

        var submittedScaleIds = scales.Select(scale => scale.Id).ToHashSet();

        foreach (var existingScale in existingScalesById.Values)
        {
            if (!submittedScaleIds.Contains(existingScale.Id))
            {
                context.RatingScales.Remove(existingScale);
            }
        }

        foreach (var scale in scales)
        {
            if (existingScalesById.TryGetValue(scale.Id, out var trackedScale))
            {
                trackedScale.Rename(scale.Name);
            }
            else
            {
                context.RatingScales.Add(scale);
            }

            var trackedPointsById = existingPointsByScale[scale.Id].ToDictionary(point => point.Id);
            var submittedPointIds = scale.Points.Select(point => point.Id).ToHashSet();

            foreach (var trackedPoint in trackedPointsById.Values)
            {
                if (!submittedPointIds.Contains(trackedPoint.Id))
                {
                    context.RatingScalePoints.Remove(trackedPoint);
                }
            }

            foreach (var point in scale.Points)
            {
                if (trackedPointsById.TryGetValue(point.Id, out var trackedPoint))
                {
                    trackedPoint.Update(point.PointCode, point.PointLabel, point.PointOrder);
                }
                else
                {
                    context.RatingScalePoints.Add(point);
                }
            }
        }

        // No SaveChangesAsync — the unit-of-work behaviour commits the transaction.
    }
}
