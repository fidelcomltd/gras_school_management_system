using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>One rating row, read-only (TASK-0083 stage 1) — for the GET grid and for computing the sheet's current derived version before a save.</summary>
/// <param name="PupilId">The pupil this rating belongs to.</param>
/// <param name="TraitId">The trait this rating is for.</param>
/// <param name="RatingScalePointId">The chosen point.</param>
public sealed record TraitRatingSnapshot(Guid PupilId, Guid TraitId, Guid RatingScalePointId);

/// <summary>Persistence port for <see cref="TraitRating"/> (TASK-0083 stage 1).</summary>
public interface ITraitRatingRepository
{
    /// <summary>Every rating for <paramref name="resultSetId"/>, read-only. <c>AsNoTracking</c>.</summary>
    Task<IReadOnlyList<TraitRatingSnapshot>> ListReadOnlyAsync(Guid resultSetId, CancellationToken cancellationToken);

    /// <summary>Every rating for <paramref name="resultSetId"/>, TRACKED, for a command that will update or remove some and add others.</summary>
    Task<IReadOnlyList<TraitRating>> ListTrackedAsync(Guid resultSetId, CancellationToken cancellationToken);

    /// <summary>Stages a brand-new rating row for insertion. Does NOT commit.</summary>
    Task AddAsync(TraitRating rating, CancellationToken cancellationToken);

    /// <summary>Removes <paramref name="rating"/> permanently — an explicit <see langword="null"/> point clears a cell (spec, Q1-A ruling). Does NOT commit.</summary>
    Task RemoveAsync(TraitRating rating, CancellationToken cancellationToken);
}
