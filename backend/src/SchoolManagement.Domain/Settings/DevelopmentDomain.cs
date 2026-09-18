using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One development domain (spec 6.2.13's "Development domains and indicators, nursery only"): a
/// named, ordered group of <see cref="DevelopmentIndicator"/> rows rated against one
/// <see cref="RatingScale"/>, for example "Personal &amp; Physical Development". Replaces nothing in
/// 6.2.7 — this configuration is new.
/// </summary>
/// <remarks>
/// <para>
/// PERSISTED WHOLE, REPLACED WHOLE, id preserved across a save — the SAME convention TASK-0072 stage 1
/// review established for <c>RatingScale</c>/<c>RatingScalePoint</c>, applied here from the start
/// rather than fixed after the fact: <c>PUT /settings/development-domains</c>
/// (<c>IDevelopmentDomainRepository.ReplaceAllAsync</c>) diffs the submitted set against what is
/// persisted, so an existing domain or indicator id never changes under an unrelated edit. This
/// entity carries no optimistic concurrency token of its own — the group's single
/// <c>SchoolProfile</c> version pointer (added stage 2b) is what a client echoes back.
/// </para>
/// <para>
/// NO EF NAVIGATION to <see cref="DevelopmentIndicator"/> — <see cref="Indicators"/> is populated by
/// <c>DevelopmentDomainRepository</c> grouping two flat, independently-queried tables in memory, the
/// same shape <c>RatingScale</c>/<c>RatingScalePoint</c> already use.
/// </para>
/// <para>
/// A domain's own removal (its id absent from a submitted set) is refused exactly like an indicator's
/// — walking its own indicators through <c>IDevelopmentIndicatorUsageGate</c> and naming the first one
/// that has ever been rated — rather than inventing a second, domain-level message spec 6.2.13 never
/// gives. Archiving (<see cref="DevelopmentDomainStatus.Archived"/>) is always allowed; it never
/// destroys data.
/// </para>
/// </remarks>
public sealed class DevelopmentDomain : Entity<Guid>
{
    /// <summary>Spec 6.2.13's field table gives no explicit length; 60 matches <see cref="RatingScale.NameMaxLength"/>.</summary>
    public const int NameMaxLength = 60;

    // EF Core materialisation constructor.
    private DevelopmentDomain()
        : base()
    {
        Name = null!;
        Indicators = [];
    }

    private DevelopmentDomain(
        Guid id,
        Guid sectionId,
        string name,
        int displayOrder,
        Guid ratingScaleId,
        bool allowsIndicatorComment,
        DevelopmentDomainStatus status,
        IReadOnlyList<DevelopmentIndicator> indicators)
        : base(id)
    {
        SectionId = sectionId;
        Name = name;
        DisplayOrder = displayOrder;
        RatingScaleId = ratingScaleId;
        AllowsIndicatorComment = allowsIndicatorComment;
        Status = status;
        Indicators = indicators;
    }

    /// <summary>The owning <see cref="Classes.Section"/> (spec 6.2.13: "Domains and indicators are section-scoped").</summary>
    public Guid SectionId { get; private set; }

    /// <summary>Up to <see cref="NameMaxLength"/> characters, trimmed. Printed as the block heading.</summary>
    public string Name { get; private set; }

    /// <summary>Printed block order within the section (E.3: "Four blocks, in this order").</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>The <see cref="RatingScale"/> this domain's indicators are rated against. One rating block, one scale.</summary>
    public Guid RatingScaleId { get; private set; }

    /// <summary>Whether the entry screen prints a per-indicator Comments column for this domain (spec 6.2.13).</summary>
    public bool AllowsIndicatorComment { get; private set; }

    /// <summary>Active or archived (spec 6.2.13). An archived domain leaves new entry screens but stays on historical sheets.</summary>
    public DevelopmentDomainStatus Status { get; private set; }

    /// <summary>Every indicator on this domain, ordered by <see cref="DevelopmentIndicator.DisplayOrder"/>. Never null; empty only transiently during validation.</summary>
    public IReadOnlyList<DevelopmentIndicator> Indicators { get; private set; }

    /// <summary>
    /// Builds one domain with its indicators already attached. Trusts its input — the whole-set
    /// validator (stage 2b) runs before any domain is constructed, the same convention
    /// <see cref="RatingScale.Create"/> follows.
    /// </summary>
    public static DevelopmentDomain Create(
        Guid id,
        Guid sectionId,
        string name,
        int displayOrder,
        Guid ratingScaleId,
        bool allowsIndicatorComment,
        DevelopmentDomainStatus status,
        IReadOnlyList<DevelopmentIndicator> indicators) =>
        new(id, sectionId, name.Trim(), displayOrder, ratingScaleId, allowsIndicatorComment, status, indicators);

    /// <summary>
    /// Updates an EXISTING, tracked domain in place, id preserved — same convention as
    /// <see cref="RatingScale.Rename"/>/<see cref="RatingScalePoint.Update"/>. Used only by
    /// <c>DevelopmentDomainRepository.ReplaceAllAsync</c> against a row already loaded from the
    /// database, whose id a submitted domain echoed back. <see cref="Indicators"/> is diffed
    /// separately by the repository, not through this method.
    /// </summary>
    public void Update(
        Guid sectionId,
        string name,
        int displayOrder,
        Guid ratingScaleId,
        bool allowsIndicatorComment,
        DevelopmentDomainStatus status)
    {
        SectionId = sectionId;
        Name = name.Trim();
        DisplayOrder = displayOrder;
        RatingScaleId = ratingScaleId;
        AllowsIndicatorComment = allowsIndicatorComment;
        Status = status;
    }
}
