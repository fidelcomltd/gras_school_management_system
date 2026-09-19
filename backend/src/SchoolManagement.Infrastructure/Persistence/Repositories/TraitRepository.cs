using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ITraitRepository"/>. <c>trait</c> is a single flat table (no
/// child rows, unlike <see cref="DevelopmentDomain"/>/<see cref="DevelopmentIndicator"/>); <c>trait_block</c>
/// is a fixed two-row table this repository only ever UPDATES, never inserts into or deletes from.
/// </summary>
internal sealed class TraitRepository(ApplicationDbContext context) : ITraitRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Trait>> ListReadOnlyOrderedAsync(CancellationToken cancellationToken) =>
        await context.Traits
            .AsNoTracking()
            .OrderBy(trait => trait.Domain)
            .ThenBy(trait => trait.DisplayOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TraitBlock>> ListBlocksReadOnlyAsync(CancellationToken cancellationToken) =>
        await context.TraitBlocks
            .AsNoTracking()
            .OrderBy(block => block.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks>
    /// Id-stable from the start — same convention <c>DevelopmentDomainRepository.ReplaceAllAsync</c>
    /// applies (itself applying TASK-0072 stage 1's review-fix lesson from the start rather than fixing
    /// it after the fact): a trait whose id matches an existing row is UPDATED IN PLACE (id preserved,
    /// via <see cref="Trait.Update"/> on the TRACKED entity, so EF issues an <c>UPDATE</c>, never a
    /// <c>DELETE</c>+<c>INSERT</c> of the same id); an existing row whose id is absent from
    /// <paramref name="traits"/> is removed; anything with no matching id is a genuinely new row. The
    /// caller (<c>UpdateTraitsCommandHandler</c>) has already resolved which submitted id is which,
    /// checked the usage gate before any removal, and rejected an unknown submitted id — this method
    /// trusts <paramref name="traits"/>' ids exactly as given.
    /// </remarks>
    public async Task ReplaceAllAsync(
        IReadOnlyList<Trait> traits,
        Guid affectiveRatingScaleId,
        Guid psychomotorRatingScaleId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(traits);

        var existingTraitsById = await context.Traits
            .ToDictionaryAsync(trait => trait.Id, cancellationToken)
            .ConfigureAwait(false);

        var submittedIds = traits.Select(trait => trait.Id).ToHashSet();

        foreach (var existingTrait in existingTraitsById.Values)
        {
            if (!submittedIds.Contains(existingTrait.Id))
            {
                context.Traits.Remove(existingTrait);
            }
        }

        foreach (var trait in traits)
        {
            if (existingTraitsById.TryGetValue(trait.Id, out var trackedTrait))
            {
                trackedTrait.Update(trait.Domain, trait.Name, trait.DisplayOrder, trait.Status);
            }
            else
            {
                context.Traits.Add(trait);
            }
        }

        // trait_block ALWAYS has exactly two rows (Affective, Psychomotor), seeded by migration and
        // never inserted or removed here — only each block's RatingScaleId is ever updated, on the
        // TRACKED row, so EF issues an UPDATE, matching every other block/point/indicator convention.
        var blocksById = await context.TraitBlocks
            .ToDictionaryAsync(block => block.Id, cancellationToken)
            .ConfigureAwait(false);

        if (blocksById.TryGetValue(TraitDomain.Affective, out var affectiveBlock))
        {
            affectiveBlock.UpdateScale(affectiveRatingScaleId);
        }

        if (blocksById.TryGetValue(TraitDomain.Psychomotor, out var psychomotorBlock))
        {
            psychomotorBlock.UpdateScale(psychomotorRatingScaleId);
        }

        // No SaveChangesAsync — the unit-of-work behaviour commits the transaction.
    }
}
