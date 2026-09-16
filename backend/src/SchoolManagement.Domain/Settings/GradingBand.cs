using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One band of the grading scale (spec 6.2.5; seed data replaced by amendment 6.2.13 — nine bands,
/// not six; <see cref="GradeLetter"/> widened to <see cref="GradeLetterMaxLength"/> 3 to hold
/// <c>A+</c> and <c>B-</c>). Data model: <c>grading_band</c> (TASK-0069 follows <c>02-data-model.md</c>
/// lines 16-17's table name; 6.2.13's own prose calls the same entity <c>grade_band</c> — see
/// <c>backend/docs/ASSUMPTIONS.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// PERSISTED WHOLE, REPLACED WHOLE. <c>PUT /settings/grading</c> is spec 6.2.12's "whole scale as one
/// array, atomic" — the handler deletes every existing row and inserts the submitted set fresh
/// (<c>IGradingBandRepository.ReplaceAllAsync</c>), so this entity carries no optimistic
/// concurrency token of its own; the group's single <see cref="SchoolProfile.GradingVersionNumber"/>
/// pointer is what a client echoes back.
/// </para>
/// <para>
/// <see cref="DisplayOrder"/> is never taken from client input — 6.2.11: "Display order and bound
/// order are independent... the system sorts by lower_bound internally for grade resolution and uses
/// display_order only for printing the key." The handler assigns it from the SUBMITTED ARRAY POSITION
/// (spec 6.2.5: "System-maintained from the drag order"), so an ordered array on the wire IS the drag
/// order, with no separate field to duplicate or disagree with it.
/// </para>
/// </remarks>
public sealed class GradingBand : Entity<Guid>
{
    /// <summary>6.2.13: widened from <c>String 2</c> to hold <c>A+</c> and <c>B-</c>.</summary>
    public const int GradeLetterMaxLength = 3;

    /// <summary>6.2.5: <c>remark</c>, <c>String 40</c>.</summary>
    public const int RemarkMaxLength = 40;

    /// <summary>6.2.5 rule 10: "Every band has a remark of at least three characters."</summary>
    public const int RemarkMinLength = 3;

    // EF Core materialisation constructor.
    private GradingBand()
        : base()
    {
        GradeLetter = null!;
        Remark = null!;
    }

    private GradingBand(Guid id, int lowerBound, int upperBound, string gradeLetter, string remark, int displayOrder)
        : base(id)
    {
        LowerBound = lowerBound;
        UpperBound = upperBound;
        GradeLetter = gradeLetter;
        Remark = remark;
        DisplayOrder = displayOrder;
    }

    /// <summary>0 to 100 inclusive. Whole number — enforced by the wire type, never a decimal.</summary>
    public int LowerBound { get; private set; }

    /// <summary>0 to 100 inclusive. Greater than or equal to <see cref="LowerBound"/>.</summary>
    public int UpperBound { get; private set; }

    /// <summary>Unique across the scale, case-insensitive. For example <c>A+</c>, <c>B-</c>, <c>F</c>.</summary>
    public string GradeLetter { get; private set; }

    /// <summary>Free text, printed in the Remark column of the result sheet.</summary>
    public string Remark { get; private set; }

    /// <summary>Printing order of the grading key. Independent of <see cref="LowerBound"/> order (6.2.11).</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Builds one band. Trusts its input — <see cref="GradingScaleRules.ValidateWholeScale"/> has
    /// already validated the whole submitted set before any band is constructed.
    /// </summary>
    public static GradingBand Create(
        Guid id,
        int lowerBound,
        int upperBound,
        string gradeLetter,
        string remark,
        int displayOrder) =>
        new(id, lowerBound, upperBound, gradeLetter.Trim(), remark.Trim(), displayOrder);
}
