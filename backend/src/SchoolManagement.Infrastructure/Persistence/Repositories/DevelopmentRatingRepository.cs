using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IDevelopmentRatingRepository"/> (TASK-0083 stage 2).</summary>
internal sealed class DevelopmentRatingRepository(ApplicationDbContext context) : IDevelopmentRatingRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<DevelopmentRatingSnapshot>> ListReadOnlyAsync(Guid resultSetId, CancellationToken cancellationToken) =>
        await context.DevelopmentRatings
            .AsNoTracking()
            .Where(rating => rating.ResultSetId == resultSetId)
            .Select(rating => new DevelopmentRatingSnapshot(rating.PupilId, rating.IndicatorId, rating.RatingScalePointId, rating.Comment))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<DevelopmentRating>> ListTrackedAsync(Guid resultSetId, CancellationToken cancellationToken) =>
        await context.DevelopmentRatings
            .Where(rating => rating.ResultSetId == resultSetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task AddAsync(DevelopmentRating rating, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rating);
        cancellationToken.ThrowIfCancellationRequested();

        context.DevelopmentRatings.Add(rating);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(DevelopmentRating rating, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rating);
        cancellationToken.ThrowIfCancellationRequested();

        context.DevelopmentRatings.Remove(rating);

        return Task.CompletedTask;
    }
}
