using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ITraitRatingRepository"/> (TASK-0083 stage 1).</summary>
internal sealed class TraitRatingRepository(ApplicationDbContext context) : ITraitRatingRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TraitRatingSnapshot>> ListReadOnlyAsync(Guid resultSetId, CancellationToken cancellationToken) =>
        await context.TraitRatings
            .AsNoTracking()
            .Where(rating => rating.ResultSetId == resultSetId)
            .Select(rating => new TraitRatingSnapshot(rating.PupilId, rating.TraitId, rating.RatingScalePointId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TraitRating>> ListTrackedAsync(Guid resultSetId, CancellationToken cancellationToken) =>
        await context.TraitRatings
            .Where(rating => rating.ResultSetId == resultSetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task AddAsync(TraitRating rating, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rating);
        cancellationToken.ThrowIfCancellationRequested();

        context.TraitRatings.Add(rating);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(TraitRating rating, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rating);
        cancellationToken.ThrowIfCancellationRequested();

        context.TraitRatings.Remove(rating);

        return Task.CompletedTask;
    }
}
