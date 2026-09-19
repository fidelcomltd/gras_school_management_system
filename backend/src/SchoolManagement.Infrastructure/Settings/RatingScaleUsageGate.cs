using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// Real implementation of <see cref="IRatingScaleUsageGate"/> (TASK-0072 stage 2a) — a scale is in use
/// when any <c>development_domain</c> row, or either <c>trait_block</c> row (stage 3b), references it.
/// Replaces the stage 1 stand-in that unconditionally answered "not in use", now that
/// <c>development_domain.rating_scale_id</c>/<c>trait_block.rating_scale_id</c> exist to query, per the
/// port's own documented seam.
/// </summary>
internal sealed class RatingScaleUsageGate(ApplicationDbContext context) : IRatingScaleUsageGate
{
    /// <inheritdoc />
    public async Task<bool> IsInUseAsync(Guid ratingScaleId, CancellationToken cancellationToken)
    {
        var usedByDevelopmentDomain = await context.DevelopmentDomains
            .AsNoTracking()
            .AnyAsync(domain => domain.RatingScaleId == ratingScaleId, cancellationToken)
            .ConfigureAwait(false);

        if (usedByDevelopmentDomain)
        {
            return true;
        }

        return await context.TraitBlocks
            .AsNoTracking()
            .AnyAsync(block => block.RatingScaleId == ratingScaleId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsPointRatedAsync(Guid pointId, CancellationToken cancellationToken)
    {
        var ratedByATrait = await context.TraitRatings
            .AsNoTracking()
            .AnyAsync(rating => rating.RatingScalePointId == pointId, cancellationToken)
            .ConfigureAwait(false);

        if (ratedByATrait)
        {
            return true;
        }

        return await context.DevelopmentRatings
            .AsNoTracking()
            .AnyAsync(rating => rating.RatingScalePointId == pointId, cancellationToken)
            .ConfigureAwait(false);
    }
}
