using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One column of the continuous-assessment/examination structure (spec 6.2.6; seed data replaced by
/// amendment 6.2.13 — three components, 1st CA 20 / 2nd CA 20 / Exam 60, not four).
/// </summary>
/// <remarks>
/// <para>
/// <b>NOTHING MAY ASSUME A COMPONENT COUNT</b> (6.2.13's durability requirement). The continuous
/// assessment total is the sum of every component where <see cref="IsExamination"/> is
/// <see langword="false"/> — computed, never a literal 40, wherever it is needed. Column headings are
/// composed from <see cref="Name"/> and <see cref="MaxMark"/>.
/// </para>
/// <para>
/// <c>Id</c> IS THE MARK-STORAGE KEY (6.2.6: "the marks are stored against the component id"),
/// which is why <c>PUT /settings/assessment</c>'s replace is ID-AWARE, not a blind delete-and-recreate
/// like <see cref="GradingBand"/>'s: a submitted item carrying an existing id is an edit to that same
/// row (rename/reorder always safe; changing <see cref="MaxMark"/> or <see cref="IsExamination"/> safe
/// only before the session lock engages); a submitted item with no id, or an existing id absent from
/// the submission, is an add or a remove, both blocked once
/// <c>ISubjectScoreSessionLockLookup.AnyScoreExistsInSessionAsync</c> reports a mark entered anywhere
/// in the active session (see that interface's remarks for why it is honestly <see langword="false"/>
/// today).
/// </para>
/// <para>
/// <see cref="DisplayOrder"/> is never taken from client input for the SAME reason as
/// <see cref="GradingBand.DisplayOrder"/> — array position on the wire IS the order — with one
/// addition: the examination component is always forced to the last position regardless of where it
/// appears in the submitted array (spec 6.2.6).
/// </para>
/// </remarks>
public sealed class AssessmentComponent : Entity<Guid>
{
    /// <summary>6.2.6: <c>name</c>, <c>String 40</c>. Unique across components, case-insensitive.</summary>
    public const int NameMaxLength = 40;

    /// <summary>6.2.6: <c>short_label</c>, <c>String 12</c>. Unique across components.</summary>
    public const int ShortLabelMaxLength = 12;

    /// <summary>6.2.6: <c>max_mark</c> floor. "Every maximum must be worth at least 1 mark."</summary>
    public const int MaxMarkMinimum = 1;

    /// <summary>6.2.6: <c>max_mark</c> ceiling.</summary>
    public const int MaxMarkCeiling = 100;

    // EF Core materialisation constructor.
    private AssessmentComponent()
        : base()
    {
        Name = null!;
        ShortLabel = null!;
    }

    private AssessmentComponent(
        Guid id,
        string name,
        string shortLabel,
        int maxMark,
        bool isExamination,
        int displayOrder)
        : base(id)
    {
        Name = name;
        ShortLabel = shortLabel;
        MaxMark = maxMark;
        IsExamination = isExamination;
        DisplayOrder = displayOrder;
    }

    /// <summary>For example "1st CA", "2nd CA", "Exam". Unique across components, case-insensitive.</summary>
    public string Name { get; private set; }

    /// <summary>Used as the column header where space is tight. Unique across components.</summary>
    public string ShortLabel { get; private set; }

    /// <summary>1 to 100. Together with every other component's, sums to exactly 100.</summary>
    public int MaxMark { get; private set; }

    /// <summary>Exactly one component in the structure has this <see langword="true"/>.</summary>
    public bool IsExamination { get; private set; }

    /// <summary>Print/entry-grid column order. The examination is always forced last.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Builds one component. Trusts its input —
    /// <see cref="AssessmentStructureRules.ValidateWholeStructure"/> has already validated the whole
    /// submitted set.
    /// </summary>
    public static AssessmentComponent Create(
        Guid id,
        string name,
        string shortLabel,
        int maxMark,
        bool isExamination,
        int displayOrder) =>
        new(id, name.Trim(), shortLabel.Trim(), maxMark, isExamination, displayOrder);

    /// <summary>
    /// Applies a rename/reorder in place, preserving <see cref="Entity{TId}.Id"/> — always safe, even
    /// under the session lock (6.2.6). <see cref="MaxMark"/> and <see cref="IsExamination"/> are NOT
    /// parameters here on purpose: changing either goes through <see cref="ChangeMaximumAndKind"/>,
    /// the one path the session lock actually guards.
    /// </summary>
    public void Rename(string name, string shortLabel, int displayOrder)
    {
        Name = name.Trim();
        ShortLabel = shortLabel.Trim();
        DisplayOrder = displayOrder;
    }

    /// <summary>
    /// Applies a maximum-mark and/or examination-flag change. The caller (the handler) is responsible
    /// for having already rejected this call under the session lock — this method trusts it was safe
    /// to call.
    /// </summary>
    public void ChangeMaximumAndKind(int maxMark, bool isExamination, int displayOrder)
    {
        MaxMark = maxMark;
        IsExamination = isExamination;
        DisplayOrder = displayOrder;
    }
}
