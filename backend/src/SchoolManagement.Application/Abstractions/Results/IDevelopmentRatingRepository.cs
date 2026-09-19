using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>One rating row, read-only (TASK-0083 stage 2) — for the GET grid and for computing the sheet's current derived version before a save.</summary>
/// <param name="PupilId">The pupil this rating belongs to.</param>
/// <param name="IndicatorId">The indicator this rating is for.</param>
/// <param name="RatingScalePointId">The chosen point.</param>
/// <param name="Comment">Optional, up to <see cref="DevelopmentRating.CommentMaxLength"/> characters.</param>
public sealed record DevelopmentRatingSnapshot(Guid PupilId, Guid IndicatorId, Guid RatingScalePointId, string? Comment);

/// <summary>Persistence port for <see cref="DevelopmentRating"/> (TASK-0083 stage 2).</summary>
public interface IDevelopmentRatingRepository
{
    /// <summary>Every rating for <paramref name="resultSetId"/>, read-only. <c>AsNoTracking</c>.</summary>
    Task<IReadOnlyList<DevelopmentRatingSnapshot>> ListReadOnlyAsync(Guid resultSetId, CancellationToken cancellationToken);

    /// <summary>Every rating for <paramref name="resultSetId"/>, TRACKED, for a command that will update or remove some and add others.</summary>
    Task<IReadOnlyList<DevelopmentRating>> ListTrackedAsync(Guid resultSetId, CancellationToken cancellationToken);

    /// <summary>Stages a brand-new rating row for insertion. Does NOT commit.</summary>
    Task AddAsync(DevelopmentRating rating, CancellationToken cancellationToken);

    /// <summary>Removes <paramref name="rating"/> permanently — clearing the point clears the whole row, comment included (Q3-A). Does NOT commit.</summary>
    Task RemoveAsync(DevelopmentRating rating, CancellationToken cancellationToken);
}
