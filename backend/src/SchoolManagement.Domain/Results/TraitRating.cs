using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Results;

/// <summary>
/// One pupil's rating on one trait, for one result set (spec 09 §6.7.3's <c>trait_rating</c>;
/// TASK-0083 stage 1). Entered by the class teacher on the primary trait grid (spec §6.7.7,
/// §6.7.12 amendment) — never converted to a mark, never computed.
/// </summary>
/// <remarks>
/// <para>
/// UNIQUE ON <c>(result_set_id, pupil_id, trait_id)</c> — one rating per pupil per trait per result
/// set, the same shape <see cref="SubjectScore"/> uses for marks, minus the void concept: a trait
/// rating is never voided, only cleared (deleted) or replaced.
/// </para>
/// <para>
/// <see cref="RatingScalePointId"/> is a bare FK to <c>rating_scale_point</c> — WHICH scale a point
/// must belong to (the trait's block: affective or psychomotor) is data-dependent (it comes from
/// <c>TraitBlock.RatingScaleId</c>, resolved at save time) and so is the caller's job
/// (<c>SaveTraitRatingsHandler</c>), the same "entity trusts its input" posture
/// <see cref="SubjectScore.Create"/> takes for component-mark ranges.
/// </para>
/// </remarks>
public sealed class TraitRating : Entity<Guid>, IAuditableEntity
{
    private TraitRating(Guid id, Guid resultSetId, Guid pupilId, Guid traitId, Guid ratingScalePointId)
        : base(id)
    {
        ResultSetId = resultSetId;
        PupilId = pupilId;
        TraitId = traitId;
        RatingScalePointId = ratingScalePointId;
    }

    // EF Core materialisation constructor.
    private TraitRating()
        : base()
    {
    }

    /// <summary>The result set this rating belongs to (denormalised, same convention as <see cref="SubjectScore.ResultSetId"/>).</summary>
    public Guid ResultSetId { get; private set; }

    /// <summary>Unique together with <see cref="TraitId"/> within <see cref="ResultSetId"/>.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>The trait being rated. Must be ACTIVE at save time — the caller checks this.</summary>
    public Guid TraitId { get; private set; }

    /// <summary>
    /// The chosen point. Must belong to the scale the trait's block currently points at — the caller
    /// checks this; this entity does not know which block a trait belongs to.
    /// </summary>
    public Guid RatingScalePointId { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>Creates a new rating row. The caller has already validated every rule — this entity trusts its input, same posture as <see cref="SubjectScore.Create"/>.</summary>
    public static Result<TraitRating> Create(Guid id, Guid resultSetId, Guid pupilId, Guid traitId, Guid ratingScalePointId)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<TraitRating>(Error.Validation("trait_rating.id_required", "Id must not be empty."));
        }

        if (resultSetId == Guid.Empty || pupilId == Guid.Empty || traitId == Guid.Empty || ratingScalePointId == Guid.Empty)
        {
            return Result.Failure<TraitRating>(Error.Validation(
                "trait_rating.reference_required",
                "ResultSetId, PupilId, TraitId and RatingScalePointId must not be empty."));
        }

        return Result.Success(new TraitRating(id, resultSetId, pupilId, traitId, ratingScalePointId));
    }

    /// <summary>Applies a new point to an EXISTING row. The caller has already validated it. Id, pupil and trait never change.</summary>
    public void UpdatePoint(Guid ratingScalePointId) => RatingScalePointId = ratingScalePointId;
}
