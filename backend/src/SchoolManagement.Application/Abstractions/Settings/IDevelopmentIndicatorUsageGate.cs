namespace SchoolManagement.Application.Abstractions.Settings;

/// <summary>
/// Whether a development indicator has ever been rated — spec 6.2.13: "Removing an indicator that has
/// ever been rated is refused in favour of archiving," with the exact message "Ratings have already
/// been entered for {indicator} this term. Archive the indicator instead, which keeps it on this
/// term's sheets and removes it from next term."
/// </summary>
/// <remarks>
/// TASK-0072 STAGE 2A's stand-in honestly answers "never rated"
/// unconditionally (<c>SchoolManagement.Infrastructure.Settings.DevelopmentIndicatorUsageGate</c>) — no
/// <c>development_indicator_rating</c> table exists yet, since Phase 3's entry screens have not been
/// built. Same documented-stand-in shape as <c>IPublishedResultsGate</c> before <c>result_set</c>
/// existed, and <c>IRatingScaleUsageGate</c> before any rating block referenced a scale. Phase 3 (the
/// non-academic input card) replaces this Infrastructure implementation with a real query once the
/// rating table exists; this port's signature does not need to change.
/// </remarks>
public interface IDevelopmentIndicatorUsageGate
{
    /// <summary>
    /// Whether the indicator identified by <paramref name="indicatorId"/> has ever been rated, in any
    /// term.
    /// </summary>
    /// <param name="indicatorId">The indicator to check.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<bool> HasEverBeenRatedAsync(Guid indicatorId, CancellationToken cancellationToken);

    /// <summary>
    /// R2 (TASK-0083 stage 3, human ruling): whether any of <paramref name="indicatorIds"/> (one
    /// domain's indicators) has a <c>development_rating</c> on a point that belongs to
    /// <paramref name="ratingScaleId"/>, in a result set that is NOT Published — "open" per the
    /// ruling's own wording. Used only when a domain's <c>ratingScaleId</c> is about to CHANGE, to
    /// decide whether that change is refused; a rating that exists only in a Published set does not
    /// block the change.
    /// </summary>
    /// <param name="indicatorIds">Every indicator currently on the domain whose scale is changing.</param>
    /// <param name="ratingScaleId">The domain's CURRENT (about-to-be-replaced) scale.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<bool> HasOpenRatingOnScaleAsync(IReadOnlyCollection<Guid> indicatorIds, Guid ratingScaleId, CancellationToken cancellationToken);
}
