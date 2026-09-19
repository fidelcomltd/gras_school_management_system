using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Results;

/// <summary>
/// One pupil's rating on one development indicator, for one result set (spec 02 §5.1's
/// <c>development_rating</c>; TASK-0083 stage 2). Entered by the nursery class teacher on the
/// development-domain grid (spec §6.7.7, §6.7.12 amendment, Appendix E.3) — never converted to a
/// mark, never computed. Nursery only: <see cref="TraitRating"/> is this entity's primary-section
/// counterpart, kept as a SEPARATE table rather than one polymorphic table, same reasoning as the two
/// usage gates TASK-0072 shipped separately.
/// </summary>
/// <remarks>
/// <para>
/// UNIQUE ON <c>(result_set_id, pupil_id, indicator_id)</c> — one rating per pupil per indicator per
/// result set, the same shape <see cref="TraitRating"/> uses.
/// </para>
/// <para>
/// <see cref="RatingScalePointId"/> is a bare FK to <c>rating_scale_point</c> — WHICH scale a point
/// must belong to (the indicator's domain) is data-dependent (resolved at save time from
/// <c>DevelopmentDomain.RatingScaleId</c>) and so is the caller's job
/// (<c>SaveDevelopmentRatingsHandler</c>), same "entity trusts its input" posture as
/// <see cref="TraitRating"/>.
/// </para>
/// <para>
/// <see cref="Comment"/> is Appendix E.3's per-indicator free text, unique to the nursery sheet — the
/// primary trait blocks have no comment column. A row is never persisted with a null
/// <see cref="RatingScalePointId"/> — Q3-A's ruling ("a comment requires a point") means a cell with
/// no point is never written at all; clearing the point deletes the whole row, comment included.
/// </para>
/// </remarks>
public sealed class DevelopmentRating : Entity<Guid>, IAuditableEntity
{
    /// <summary>Appendix E.3: "Free text up to 120 characters."</summary>
    public const int CommentMaxLength = 120;

    private DevelopmentRating(Guid id, Guid resultSetId, Guid pupilId, Guid indicatorId, Guid ratingScalePointId, string? comment)
        : base(id)
    {
        ResultSetId = resultSetId;
        PupilId = pupilId;
        IndicatorId = indicatorId;
        RatingScalePointId = ratingScalePointId;
        Comment = comment;
    }

    // EF Core materialisation constructor.
    private DevelopmentRating()
        : base()
    {
    }

    /// <summary>The result set this rating belongs to (denormalised, same convention as <see cref="TraitRating.ResultSetId"/>).</summary>
    public Guid ResultSetId { get; private set; }

    /// <summary>Unique together with <see cref="IndicatorId"/> within <see cref="ResultSetId"/>.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>The indicator being rated. Must be ACTIVE, on an ACTIVE domain, at save time — the caller checks this.</summary>
    public Guid IndicatorId { get; private set; }

    /// <summary>
    /// The chosen point. Must belong to the scale the indicator's domain currently points at — the
    /// caller checks this; this entity does not know which domain an indicator belongs to.
    /// </summary>
    public Guid RatingScalePointId { get; private set; }

    /// <summary>Optional, up to <see cref="CommentMaxLength"/> characters, trimmed. Never non-null without a point (the caller enforces this before construction).</summary>
    public string? Comment { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a new rating row. The caller has already validated every rule (roster membership,
    /// indicator status, point-on-scale, comment-requires-point, comment-domain-allows-it) — this
    /// entity trusts that input the same way <see cref="TraitRating.Create"/> does, except for the
    /// comment's own length, which is a plain-value invariant this entity keeps as a backstop.
    /// </summary>
    public static Result<DevelopmentRating> Create(
        Guid id, Guid resultSetId, Guid pupilId, Guid indicatorId, Guid ratingScalePointId, string? comment)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<DevelopmentRating>(Error.Validation("development_rating.id_required", "Id must not be empty."));
        }

        if (resultSetId == Guid.Empty || pupilId == Guid.Empty || indicatorId == Guid.Empty || ratingScalePointId == Guid.Empty)
        {
            return Result.Failure<DevelopmentRating>(Error.Validation(
                "development_rating.reference_required",
                "ResultSetId, PupilId, IndicatorId and RatingScalePointId must not be empty."));
        }

        var normalizedComment = Normalize(comment);
        if (normalizedComment is { Length: > CommentMaxLength })
        {
            return Result.Failure<DevelopmentRating>(Error.Validation(
                "development_rating.comment_too_long",
                $"Comment must be {CommentMaxLength} characters or fewer."));
        }

        return Result.Success(new DevelopmentRating(id, resultSetId, pupilId, indicatorId, ratingScalePointId, normalizedComment));
    }

    /// <summary>
    /// Applies a new point and comment to an EXISTING row — always together, a whole-cell replace, the
    /// same convention <c>SaveDevelopmentRatingsHandler</c> uses for the submitted grid: there is no
    /// sub-field patch of a single rated cell. The caller has already validated the new comment's
    /// length. Id, pupil and indicator never change.
    /// </summary>
    public void UpdateRating(Guid ratingScalePointId, string? comment)
    {
        RatingScalePointId = ratingScalePointId;
        Comment = Normalize(comment);
    }

    private static string? Normalize(string? comment)
    {
        if (comment is null)
        {
            return null;
        }

        var trimmed = comment.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
