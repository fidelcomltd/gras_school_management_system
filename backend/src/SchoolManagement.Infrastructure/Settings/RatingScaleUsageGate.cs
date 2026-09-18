using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// Real implementation of <see cref="IRatingScaleUsageGate"/> (TASK-0072 stage 2a) — a scale is in use
/// when any <c>development_domain</c> row references it. Replaces the stage 1 stand-in that
/// unconditionally answered "not in use", now that <c>development_domain.rating_scale_id</c> exists to
/// query, per the port's own documented seam. Stage 3 (trait blocks) extends this with an
/// <c>OR EXISTS</c> against its own new reference column, not a second query method.
/// </summary>
internal sealed class RatingScaleUsageGate(ApplicationDbContext context) : IRatingScaleUsageGate
{
    /// <inheritdoc />
    public Task<bool> IsInUseAsync(Guid ratingScaleId, CancellationToken cancellationToken) =>
        context.DevelopmentDomains
            .AsNoTracking()
            .AnyAsync(domain => domain.RatingScaleId == ratingScaleId, cancellationToken);
}
