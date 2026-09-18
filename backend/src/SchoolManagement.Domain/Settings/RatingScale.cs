using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One rating scale (spec 6.2.13's "Rating scales become records"): a named, ordered set of points a
/// rating block chooses from. Replaces 6.2.7's single school-wide <c>trait_scale</c> — each rating
/// block (a development domain today; a trait block from TASK-0072 stages 2-3) references a scale by
/// id instead of every rating on the system sharing one.
/// </summary>
/// <remarks>
/// <para>
/// PERSISTED WHOLE, REPLACED WHOLE, same convention as <see cref="GradingBand"/>: <c>PUT
/// /settings/rating-scales</c> deletes every existing scale and point and inserts the submitted set
/// fresh (<c>IRatingScaleRepository.ReplaceAllAsync</c>). This entity carries no optimistic
/// concurrency token of its own — the group's single
/// <see cref="SchoolProfile.RatingScalesVersionNumber"/> pointer is what a client echoes back.
/// </para>
/// <para>
/// NO EF NAVIGATION to <see cref="RatingScalePoint"/> — <see cref="Points"/> is populated by
/// <c>RatingScaleRepository</c> grouping two flat, independently-queried tables in memory before
/// calling <see cref="Create"/>, the same shape <see cref="GradingBand"/> and
/// <see cref="AssessmentComponent"/> already use for their own flat tables, just extended to a second
/// table without introducing EF's private-backing-field collection machinery this codebase has no
/// other precedent for.
/// </para>
/// </remarks>
public sealed class RatingScale : Entity<Guid>
{
    /// <summary>6.2.13: no explicit length given; 60 matches every other short settings name field (grading's <c>remark</c> aside).</summary>
    public const int NameMaxLength = 60;

    // EF Core materialisation constructor.
    private RatingScale()
        : base()
    {
        Name = null!;
        Points = [];
    }

    private RatingScale(Guid id, string name, IReadOnlyList<RatingScalePoint> points)
        : base(id)
    {
        Name = name;
        Points = points;
    }

    /// <summary>For example <c>Nursery development</c>, <c>Primary trait</c>. Unique across the whole submitted set.</summary>
    public string Name { get; private set; }

    /// <summary>Every point on this scale, ordered by <see cref="RatingScalePoint.PointOrder"/>. Never null; empty only transiently during validation.</summary>
    public IReadOnlyList<RatingScalePoint> Points { get; private set; }

    /// <summary>
    /// Builds one scale with its points already attached. Trusts its input —
    /// <see cref="RatingScaleRules.ValidateWholeSet"/> has already validated the whole submitted set
    /// before any scale is constructed.
    /// </summary>
    public static RatingScale Create(Guid id, string name, IReadOnlyList<RatingScalePoint> points) =>
        new(id, name.Trim(), points);

    /// <summary>
    /// Renames an EXISTING, tracked scale in place (TASK-0072 stage 1 review fix). Used only by
    /// <c>RatingScaleRepository.ReplaceAllAsync</c> against a row already loaded from the database,
    /// whose id a submitted scale echoed back — a rename must never change the id, because stage 2/3
    /// rating blocks reference a scale BY that id.
    /// </summary>
    public void Rename(string name) => Name = name.Trim();
}
