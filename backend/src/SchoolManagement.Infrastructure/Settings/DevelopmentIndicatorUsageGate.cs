using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// Real implementation of <see cref="IDevelopmentIndicatorUsageGate"/> (TASK-0083 stage 2) — an
/// indicator has been rated when any <c>development_rating</c> row references it, in any result set,
/// ever. Replaces TASK-0072 stage 2a's stand-in that unconditionally answered "never rated", now that
/// <c>development_rating</c> exists to query, per the port's own documented seam — same shape
/// <see cref="TraitUsageGate"/> took over its own stand-in in TASK-0083 stage 1.
/// </summary>
internal sealed class DevelopmentIndicatorUsageGate(ApplicationDbContext context) : IDevelopmentIndicatorUsageGate
{
    /// <inheritdoc />
    public async Task<bool> HasEverBeenRatedAsync(Guid indicatorId, CancellationToken cancellationToken) =>
        await context.DevelopmentRatings
            .AsNoTracking()
            .AnyAsync(rating => rating.IndicatorId == indicatorId, cancellationToken)
            .ConfigureAwait(false);
}
