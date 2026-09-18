using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One of the exactly two trait blocks (spec 6.2.13's "Rating scales become records": "each rating
/// block references a scale by id"), keyed by <see cref="TraitDomain"/> rather than by a
/// <see cref="Guid"/> — there is no "create" or "remove" for this row, only which
/// <see cref="RatingScale"/> it currently points at.
/// </summary>
/// <remarks>
/// Seeded with exactly two rows by migration (Affective, Psychomotor), both pointing at the
/// <c>Primary trait</c> scale (Appendix F.3, TASK-0072 stage 3 task instruction). <c>PUT
/// /settings/traits</c> only ever UPDATES one or both rows' <see cref="RatingScaleId"/> — see
/// <c>ITraitRepository.ReplaceAllAsync</c>'s remarks for why insert/remove is never reachable here.
/// </remarks>
public sealed class TraitBlock : Entity<TraitDomain>
{
    // EF Core materialisation constructor.
    private TraitBlock()
        : base()
    {
    }

    private TraitBlock(TraitDomain domain, Guid ratingScaleId)
        : base(domain)
    {
        RatingScaleId = ratingScaleId;
    }

    /// <summary>The scale this block's traits are rated against. One block, one scale.</summary>
    public Guid RatingScaleId { get; private set; }

    /// <summary>Builds one block. Used only by the migration seed and by tests — see this type's remarks.</summary>
    public static TraitBlock Create(TraitDomain domain, Guid ratingScaleId) => new(domain, ratingScaleId);

    /// <summary>
    /// Points an EXISTING, tracked block at a different scale. Used only by
    /// <c>TraitRepository.ReplaceAllAsync</c> against a row already loaded from the database.
    /// </summary>
    public void UpdateScale(Guid ratingScaleId) => RatingScaleId = ratingScaleId;
}
