using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One indicator on a <see cref="DevelopmentDomain"/> (spec 6.2.13's "Development domains and
/// indicators, nursery only"): a single rated row on the nursery sheet's development-domain table,
/// for example <c>Potty trained</c> under Personal &amp; Physical Development.
/// </summary>
/// <remarks>
/// Same "persisted whole, replaced whole, id preserved across a save" convention TASK-0072 stage 1
/// established for <c>RatingScalePoint</c> — <c>PUT /settings/development-domains</c> diffs the
/// submitted set against what is persisted rather than blindly deleting and reinserting, because a
/// future rating (Phase 3) references an indicator BY id. NO EF navigation to
/// <see cref="DevelopmentDomain"/> — <see cref="DevelopmentDomain.Indicators"/> is populated by
/// <c>DevelopmentDomainRepository</c> grouping two flat, independently-queried tables in memory, the
/// same shape <c>RatingScale</c>/<c>RatingScalePoint</c> already use.
/// </remarks>
public sealed class DevelopmentIndicator : Entity<Guid>
{
    /// <summary>6.2.13: <c>development_indicator.name</c>. Wide enough for the longest seeded name (Appendix E.3).</summary>
    public const int NameMaxLength = 160;

    // EF Core materialisation constructor.
    private DevelopmentIndicator()
        : base()
    {
        Name = null!;
    }

    private DevelopmentIndicator(Guid id, Guid domainId, string name, int displayOrder, DevelopmentIndicatorStatus status)
        : base(id)
    {
        DomainId = domainId;
        Name = name;
        DisplayOrder = displayOrder;
        Status = status;
    }

    /// <summary>The owning domain. A plain FK column — see <see cref="DevelopmentDomain"/>'s remarks for why there is no EF navigation.</summary>
    public Guid DomainId { get; private set; }

    /// <summary>Up to <see cref="NameMaxLength"/> characters, trimmed. Printed as the row label on the sheet.</summary>
    public string Name { get; private set; }

    /// <summary>Printed order within the domain, spec 6.2.13's "an ordered list of indicators."</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Active or archived (spec 6.2.13). An archived indicator leaves new entry screens but stays on historical sheets.</summary>
    public DevelopmentIndicatorStatus Status { get; private set; }

    /// <summary>
    /// Builds one indicator. Trusts its input — the whole-set validator (stage 2b) runs before any
    /// indicator is constructed, the same convention <see cref="RatingScalePoint.Create"/> follows.
    /// </summary>
    public static DevelopmentIndicator Create(
        Guid id,
        Guid domainId,
        string name,
        int displayOrder,
        DevelopmentIndicatorStatus status) =>
        new(id, domainId, name.Trim(), displayOrder, status);

    /// <summary>
    /// Updates an EXISTING, tracked indicator in place, id and <see cref="DomainId"/> preserved — same
    /// convention as <see cref="RatingScalePoint.Update"/>. Used only by
    /// <c>DevelopmentDomainRepository.ReplaceAllAsync</c> against a row already loaded from the
    /// database, whose id a submitted indicator echoed back.
    /// </summary>
    public void Update(string name, int displayOrder, DevelopmentIndicatorStatus status)
    {
        Name = name.Trim();
        DisplayOrder = displayOrder;
        Status = status;
    }
}
