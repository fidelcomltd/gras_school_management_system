using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Domain.Results;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// Real implementation of <see cref="ITraitUsageGate"/> (TASK-0083 stage 1) — a trait has been rated
/// when any <c>trait_rating</c> row references it, in any result set, ever. Replaces TASK-0072 stage
/// 3b's stand-in that unconditionally answered "never rated", now that <c>trait_rating</c> exists to
/// query, per the port's own documented seam.
/// </summary>
internal sealed class TraitUsageGate(ApplicationDbContext context) : ITraitUsageGate
{
    /// <inheritdoc />
    public async Task<bool> HasEverBeenRatedAsync(Guid traitId, CancellationToken cancellationToken) =>
        await context.TraitRatings
            .AsNoTracking()
            .AnyAsync(rating => rating.TraitId == traitId, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<bool> HasOpenRatingOnScaleAsync(
        IReadOnlyCollection<Guid> traitIds, Guid ratingScaleId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(traitIds);

        if (traitIds.Count == 0)
        {
            return false;
        }

        return await context.TraitRatings
            .AsNoTracking()
            .Where(rating => traitIds.Contains(rating.TraitId))
            .Where(rating => context.RatingScalePoints
                .Any(point => point.Id == rating.RatingScalePointId && point.RatingScaleId == ratingScaleId))
            .Where(rating => context.ResultSets
                .Any(resultSet => resultSet.Id == rating.ResultSetId && resultSet.State != ResultSetState.Published))
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
