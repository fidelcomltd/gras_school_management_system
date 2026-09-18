using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Persistence port for rating scales (spec 6.2.13). Implemented in Infrastructure.</summary>
/// <remarks>
/// <c>PUT /settings/rating-scales</c> replaces the WHOLE set — same convention as
/// <see cref="IGradingBandRepository"/>: a scale's or point's id has no meaning that survives a save.
/// </remarks>
public interface IRatingScaleRepository
{
    /// <summary>Loads every scale, read-only, with its points attached and ordered by <see cref="RatingScalePoint.PointOrder"/>. Scales are ordered by <see cref="RatingScale.Name"/>.</summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<IReadOnlyList<RatingScale>> ListReadOnlyOrderedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces every existing scale and point with <paramref name="scales"/>. Does NOT commit — the
    /// unit-of-work behaviour does that when the command returns a successful result, in the SAME
    /// transaction as the <see cref="ConfigVersion"/> row and
    /// <see cref="SchoolProfile.IncrementRatingScalesVersion"/>.
    /// </summary>
    /// <param name="scales">The whole new set, already validated by <see cref="RatingScaleRules"/>.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task ReplaceAllAsync(IReadOnlyList<RatingScale> scales, CancellationToken cancellationToken);
}
