using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Persistence port for traits and trait blocks (spec 6.2.7 / 6.2.13). Implemented in Infrastructure.</summary>
/// <remarks>
/// <c>PUT /settings/traits</c> replaces the WHOLE trait set, id-stable from the start — same
/// convention <see cref="IDevelopmentDomainRepository"/> established (TASK-0072 stage 1's review fix,
/// applied here without needing a fix-up commit). The two <see cref="TraitBlock"/> rows are never
/// inserted or removed through this port — only their <see cref="TraitBlock.RatingScaleId"/> is ever
/// updated, because exactly two rows (<see cref="TraitDomain.Affective"/>,
/// <see cref="TraitDomain.Psychomotor"/>) exist for the product's lifetime.
/// </remarks>
public interface ITraitRepository
{
    /// <summary>Loads every trait, read-only, ordered by <see cref="Trait.Domain"/> then <see cref="Trait.DisplayOrder"/>.</summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<IReadOnlyList<Trait>> ListReadOnlyOrderedAsync(CancellationToken cancellationToken);

    /// <summary>Loads both trait blocks, read-only.</summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<IReadOnlyList<TraitBlock>> ListBlocksReadOnlyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces every existing trait with <paramref name="traits"/>, diffed by id, and points both
    /// trait blocks at <paramref name="affectiveRatingScaleId"/>/<paramref name="psychomotorRatingScaleId"/>.
    /// Does NOT commit — the unit-of-work behaviour does that when the command returns a successful
    /// result, in the SAME transaction as the <see cref="ConfigVersion"/> row and
    /// <see cref="SchoolProfile.IncrementTraitsVersion"/>.
    /// </summary>
    /// <param name="traits">The whole new set, already validated by the caller.</param>
    /// <param name="affectiveRatingScaleId">The scale the affective block's traits are rated against.</param>
    /// <param name="psychomotorRatingScaleId">The scale the psychomotor block's traits are rated against.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task ReplaceAllAsync(
        IReadOnlyList<Trait> traits,
        Guid affectiveRatingScaleId,
        Guid psychomotorRatingScaleId,
        CancellationToken cancellationToken);
}
