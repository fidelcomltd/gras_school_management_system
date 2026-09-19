namespace SchoolManagement.Application.Abstractions.Settings;

/// <summary>
/// Whether a trait has ever been rated — spec 6.2.7: "Removing a trait once ratings exist in the
/// active term is rejected," with the exact message "Ratings have already been entered for {trait}
/// this term. Archive the trait instead, which keeps it on this term's sheets and removes it from next
/// term."
/// </summary>
/// <remarks>
/// TASK-0072 STAGE 3B's stand-in honestly answers "never rated" unconditionally
/// (<c>SchoolManagement.Infrastructure.Settings.TraitUsageGate</c>) — no trait-rating table exists yet,
/// since Phase 3's entry screens have not been built. Same documented-stand-in shape as
/// <see cref="IDevelopmentIndicatorUsageGate"/> before <c>development_indicator_rating</c> existed, and
/// <see cref="SchoolManagement.Application.Abstractions.Results.IPublishedResultsGate"/> before
/// <c>result_set</c> existed. Phase 3 (the non-academic input card) replaces this Infrastructure
/// implementation with a real query once the rating table exists; this port's signature does not need
/// to change.
/// </remarks>
public interface ITraitUsageGate
{
    /// <summary>Whether the trait identified by <paramref name="traitId"/> has ever been rated, in any term.</summary>
    /// <param name="traitId">The trait to check.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<bool> HasEverBeenRatedAsync(Guid traitId, CancellationToken cancellationToken);

    /// <summary>
    /// R2 (TASK-0083 stage 3, human ruling): whether any of <paramref name="traitIds"/> (one block's
    /// traits) has a <c>trait_rating</c> on a point that belongs to <paramref name="ratingScaleId"/>,
    /// in a result set that is NOT Published — "open" per the ruling's own wording. Used only when a
    /// block's <c>affectiveRatingScaleId</c>/<c>psychomotorRatingScaleId</c> is about to CHANGE, to
    /// decide whether that change is refused; a rating that exists only in a Published set does not
    /// block the change.
    /// </summary>
    /// <param name="traitIds">Every trait currently in the block whose scale is changing.</param>
    /// <param name="ratingScaleId">The block's CURRENT (about-to-be-replaced) scale.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<bool> HasOpenRatingOnScaleAsync(IReadOnlyCollection<Guid> traitIds, Guid ratingScaleId, CancellationToken cancellationToken);
}
