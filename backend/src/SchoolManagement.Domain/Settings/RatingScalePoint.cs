using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One point on a <see cref="RatingScale"/> (spec 6.2.13). Replaces 6.2.7's <c>trait_scale_point</c>:
/// <c>point_label</c> is unchanged at 120 characters, and <c>point_code</c> (1 character — the mark
/// printed in the rating column, for example <c>E</c> or <c>5</c>) replaces the old
/// <c>scale_type</c>/<c>point_value</c> pair, which existed only to distinguish numeric from letter
/// scales — a distinction a single generic 1-character code already makes without a second field
/// (TASK-0072 stage 0 open question 2, human-approved).
/// </summary>
public sealed class RatingScalePoint : Entity<Guid>
{
    /// <summary>6.2.13: "gains a <c>point_code</c> of 1 character."</summary>
    public const int PointCodeMaxLength = 1;

    /// <summary>6.2.13: "keeps the widened <c>point_label</c> of 120 characters."</summary>
    public const int PointLabelMaxLength = 120;

    /// <summary>6.2.7's rule, carried forward unchanged by 6.2.13: "The scale must have between 2 and 9 points."</summary>
    public const int MinPointsPerScale = 2;

    /// <summary>See <see cref="MinPointsPerScale"/>.</summary>
    public const int MaxPointsPerScale = 9;

    // EF Core materialisation constructor.
    private RatingScalePoint()
        : base()
    {
        PointCode = null!;
        PointLabel = null!;
    }

    private RatingScalePoint(Guid id, Guid ratingScaleId, string pointCode, string pointLabel, int pointOrder)
        : base(id)
    {
        RatingScaleId = ratingScaleId;
        PointCode = pointCode;
        PointLabel = pointLabel;
        PointOrder = pointOrder;
    }

    /// <summary>The owning scale. A plain FK column — see <see cref="RatingScale"/>'s remarks for why there is no EF navigation.</summary>
    public Guid RatingScaleId { get; private set; }

    /// <summary>The mark printed in the rating column, for example <c>E</c>, <c>N</c>, <c>5</c>. Unique within its scale.</summary>
    public string PointCode { get; private set; }

    /// <summary>The legend text for this point, printed once beside the rating block. Long enough for a full sentence.</summary>
    public string PointLabel { get; private set; }

    /// <summary>Ascending from worst to best (6.2.7, carried forward). Unique within its scale.</summary>
    public int PointOrder { get; private set; }

    /// <summary>
    /// Builds one point. Trusts its input — <see cref="RatingScaleRules.ValidateWholeSet"/> has
    /// already validated the whole submitted set before any point is constructed.
    /// </summary>
    public static RatingScalePoint Create(Guid id, Guid ratingScaleId, string pointCode, string pointLabel, int pointOrder) =>
        new(id, ratingScaleId, pointCode.Trim(), pointLabel.Trim(), pointOrder);
}
