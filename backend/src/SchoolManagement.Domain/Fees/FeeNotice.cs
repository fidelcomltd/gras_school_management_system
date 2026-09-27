using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Fees;

/// <summary>What a fee line prints (spec 6.2.13): a configured amount, or the pupil's own outstanding figure.</summary>
public enum FeeLabelKind
{
    /// <summary>An amount set per class level per term on the fee notice grid.</summary>
    Amount,

    /// <summary>The one per-pupil line, typed against the pupil's result set. At most one per section.</summary>
    Outstanding,
}

/// <summary>
/// One line of a section's next-term fee notice (spec 6.2.13, E.6, F.5). A printed notice, not a finance module: no
/// invoice, receipt, payment or balance is attached to it. Labels are section-scoped; their amounts are per term.
/// </summary>
public sealed class FeeLabel : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.2.13: String 60.</summary>
    public const int LabelMaxLength = 60;

    /// <summary>A generous bound on a section's lines; the printed block is a short table.</summary>
    public const int MaxLabelsPerSection = 20;

    /// <summary>Spec 6.2.13's seeded lines, offered to a section that has saved none.</summary>
    public static readonly IReadOnlyList<(string Label, FeeLabelKind Kind)> Defaults =
    [
        ("Tuition Fee", FeeLabelKind.Amount),
        ("Exam & PTA", FeeLabelKind.Amount),
        ("Books", FeeLabelKind.Amount),
        ("Toiletries", FeeLabelKind.Amount),
        ("Party Fee", FeeLabelKind.Amount),
        ("Outstanding Fee", FeeLabelKind.Outstanding),
    ];

    private FeeLabel(Guid id, Guid sectionId, string label, int displayOrder, FeeLabelKind kind, bool showOnPortal)
        : base(id)
    {
        SectionId = sectionId;
        Label = label;
        DisplayOrder = displayOrder;
        Kind = kind;
        ShowOnPortal = showOnPortal;
    }

    // EF Core materialisation constructor.
    private FeeLabel()
        : base()
    {
        Label = string.Empty;
    }

    /// <summary>The section whose sheets print it.</summary>
    public Guid SectionId { get; private set; }

    /// <summary>As printed.</summary>
    public string Label { get; private set; }

    /// <summary>The order printed.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Amount or outstanding.</summary>
    public FeeLabelKind Kind { get; private set; }

    /// <summary>
    /// Outstanding line only: whether the parent portal shows the figure. Off by default, so a fee dispute does not travel
    /// with a result sheet (spec 6.2.13); always false on an amount line.
    /// </summary>
    public bool ShowOnPortal { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>A new line for a section.</summary>
    public static Result<FeeLabel> Create(Guid id, Guid sectionId, string? label, int displayOrder, FeeLabelKind kind, bool showOnPortal)
    {
        var cleaned = Clean(label);
        if (cleaned.IsFailure)
        {
            return Result.Failure<FeeLabel>(cleaned.Error);
        }

        return Result.Success(new FeeLabel(id, sectionId, cleaned.Value, displayOrder, kind, kind == FeeLabelKind.Outstanding && showOnPortal));
    }

    /// <summary>Renames, reorders and (outstanding line only) sets the portal switch. The kind never changes.</summary>
    public Result Update(string? label, int displayOrder, bool showOnPortal)
    {
        var cleaned = Clean(label);
        if (cleaned.IsFailure)
        {
            return cleaned;
        }

        Label = cleaned.Value;
        DisplayOrder = displayOrder;
        ShowOnPortal = Kind == FeeLabelKind.Outstanding && showOnPortal;
        return Result.Success();
    }

    private static Result<string> Clean(string? label)
    {
        var cleaned = label?.Trim() ?? string.Empty;
        if (cleaned.Length == 0 || cleaned.Length > LabelMaxLength)
        {
            return Result.Failure<string>(Error.Validation("fee.label_invalid", $"Every fee line needs a label of 1 to {LabelMaxLength} characters."));
        }

        return Result.Success(cleaned);
    }
}

/// <summary>One amount on the fee notice grid: a line, for one class level, printed on one term's sheets.</summary>
public sealed class FeeAmount : Entity<Guid>, IAuditableEntity
{
    /// <summary>Naira, no kobo. A bound well above any school fee, to catch a slipped key rather than police prices.</summary>
    public const int MaxAmount = 100_000_000;

    private FeeAmount(Guid id, Guid feeLabelId, Guid termId, Guid classLevelId, int amount)
        : base(id)
    {
        FeeLabelId = feeLabelId;
        TermId = termId;
        ClassLevelId = classLevelId;
        Amount = amount;
    }

    // EF Core materialisation constructor.
    private FeeAmount()
        : base()
    {
    }

    /// <summary>The line.</summary>
    public Guid FeeLabelId { get; private set; }

    /// <summary>
    /// The term whose result sheets print this notice (human ruling 2026-09-27): First Term's sheets carry what to pay for
    /// Second Term, so Third Term's sheets work before the next session exists.
    /// </summary>
    public Guid TermId { get; private set; }

    /// <summary>The class level.</summary>
    public Guid ClassLevelId { get; private set; }

    /// <summary>Naira. Zero prints as a dash.</summary>
    public int Amount { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>A cell of the grid.</summary>
    public static Result<FeeAmount> Create(Guid id, Guid feeLabelId, Guid termId, Guid classLevelId, int amount) =>
        CheckAmount(amount) is { IsFailure: true } failed
            ? Result.Failure<FeeAmount>(failed.Error)
            : Result.Success(new FeeAmount(id, feeLabelId, termId, classLevelId, amount));

    /// <summary>A new figure for the cell.</summary>
    public Result Change(int amount)
    {
        var check = CheckAmount(amount);
        if (check.IsSuccess)
        {
            Amount = amount;
        }

        return check;
    }

    /// <summary>Zero to <see cref="MaxAmount"/> naira.</summary>
    public static Result CheckAmount(int amount) =>
        amount is < 0 or > MaxAmount
            ? Result.Failure(Error.Validation("fee.amount_out_of_range", $"A fee amount must be between 0 and {MaxAmount:N0} naira."))
            : Result.Success();
}

/// <summary>
/// A pupil's outstanding-fee figure for one result set (spec 6.2.13): typed from the school's own records, with no
/// relationship to any other term's figure. Blank (no row) prints as a dash. Locked once the set is published (human
/// ruling 2026-09-27).
/// </summary>
public sealed class OutstandingFee : Entity<Guid>, IAuditableEntity
{
    private OutstandingFee(Guid id, Guid resultSetId, Guid pupilId, int amount)
        : base(id)
    {
        ResultSetId = resultSetId;
        PupilId = pupilId;
        Amount = amount;
    }

    // EF Core materialisation constructor.
    private OutstandingFee()
        : base()
    {
    }

    /// <summary>The arm-term result set.</summary>
    public Guid ResultSetId { get; private set; }

    /// <summary>The pupil.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>Naira.</summary>
    public int Amount { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>A figure for a pupil.</summary>
    public static Result<OutstandingFee> Create(Guid id, Guid resultSetId, Guid pupilId, int amount) =>
        FeeAmount.CheckAmount(amount) is { IsFailure: true } failed
            ? Result.Failure<OutstandingFee>(failed.Error)
            : Result.Success(new OutstandingFee(id, resultSetId, pupilId, amount));

    /// <summary>A new figure.</summary>
    public Result Change(int amount)
    {
        var check = FeeAmount.CheckAmount(amount);
        if (check.IsSuccess)
        {
            Amount = amount;
        }

        return check;
    }
}
