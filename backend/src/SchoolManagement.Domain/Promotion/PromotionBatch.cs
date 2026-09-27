using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Promotion;

/// <summary>A committed promotion's lifecycle (spec 6.3.7). A reversed batch is kept, marked, never deleted.</summary>
public enum PromotionBatchState
{
    /// <summary>Applied: the enrolments it opened are live.</summary>
    Committed,

    /// <summary>Undone by a Super Admin with a reason; its enrolments were removed and the old ones reopened.</summary>
    Reversed,
}

/// <summary>What happened to one pupil in a promotion batch.</summary>
public enum PromotionDecisionOutcome
{
    /// <summary>Moved to an arm of the next level in the new session.</summary>
    Promoted,

    /// <summary>Stayed at the same level, in an arm of that level in the new session.</summary>
    Repeat,

    /// <summary>Moved up despite falling short: never proposed, only chosen by a holder of <c>promotion.decide</c> with a reason.</summary>
    PromotedOnTrial,

    /// <summary>Left the terminal level: status graduated, no new enrolment.</summary>
    Graduated,
}

/// <summary>
/// One run of end-of-session promotion (spec 6.3.7): from a closed session into the next, with one
/// <see cref="PromotionDecision"/> per pupil. At most one committed batch exists per source session; reversal marks this
/// row and keeps it, so the history of what was done and undone survives.
/// </summary>
public sealed class PromotionBatch : Entity<Guid>
{
    /// <summary>A reversal reason's bounds, as every other reasoned action in the product.</summary>
    public const int ReasonMinLength = 10;

    /// <summary>A reversal reason's bounds.</summary>
    public const int ReasonMaxLength = 500;

    private readonly List<PromotionDecision> _decisions = [];

    private PromotionBatch(Guid id, Guid sourceSessionId, Guid targetSessionId, DateTimeOffset committedAtUtc, string? committedBy)
        : base(id)
    {
        SourceSessionId = sourceSessionId;
        TargetSessionId = targetSessionId;
        State = PromotionBatchState.Committed;
        CommittedAtUtc = committedAtUtc;
        CommittedBy = committedBy;
    }

    // EF Core materialisation constructor.
    private PromotionBatch()
        : base()
    {
    }

    /// <summary>The session promoted from.</summary>
    public Guid SourceSessionId { get; private set; }

    /// <summary>The session the pupils were enrolled into.</summary>
    public Guid TargetSessionId { get; private set; }

    /// <summary>Committed, or reversed.</summary>
    public PromotionBatchState State { get; private set; }

    /// <summary>When it was committed.</summary>
    public DateTimeOffset CommittedAtUtc { get; private set; }

    /// <summary>The account that committed it.</summary>
    public string? CommittedBy { get; private set; }

    /// <summary>When it was reversed, if it was.</summary>
    public DateTimeOffset? ReversedAtUtc { get; private set; }

    /// <summary>The Super Admin who reversed it.</summary>
    public string? ReversedBy { get; private set; }

    /// <summary>Why it was reversed.</summary>
    public string? ReversalReason { get; private set; }

    /// <summary>One per pupil.</summary>
    public IReadOnlyList<PromotionDecision> Decisions => _decisions;

    /// <summary>A committed batch; decisions are added before it is saved.</summary>
    public static PromotionBatch Commit(Guid id, Guid sourceSessionId, Guid targetSessionId, DateTimeOffset committedAtUtc, string? committedBy) =>
        new(id, sourceSessionId, targetSessionId, committedAtUtc, committedBy);

    /// <summary>Records one pupil's decision in this batch.</summary>
    public void Add(PromotionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        _decisions.Add(decision);
    }

    /// <summary>Marks the batch reversed. The caller has undone every decision's effects first.</summary>
    public Result Reverse(string? reason, DateTimeOffset reversedAtUtc, string? reversedBy)
    {
        if (State == PromotionBatchState.Reversed)
        {
            return Result.Failure(Error.Conflict("promotion.already_reversed", "This promotion has already been reversed."));
        }

        var cleaned = reason?.Trim();
        if (cleaned is null || cleaned.Length < ReasonMinLength || cleaned.Length > ReasonMaxLength)
        {
            return Result.Failure(Error.Validation(
                "promotion.reason_required", $"Give a reason of {ReasonMinLength} to {ReasonMaxLength} characters for reversing the promotion."));
        }

        State = PromotionBatchState.Reversed;
        ReversedAtUtc = reversedAtUtc;
        ReversedBy = reversedBy;
        ReversalReason = cleaned;
        return Result.Success();
    }
}

/// <summary>One pupil's line in a <see cref="PromotionBatch"/>, with the enrolments it touched so reversal can undo it exactly.</summary>
public sealed class PromotionDecision : Entity<Guid>
{
    private PromotionDecision(
        Guid id, Guid batchId, Guid pupilId, Guid fromArmId, PromotionDecisionOutcome? proposedOutcome, PromotionDecisionOutcome outcome,
        Guid? targetArmId, string? reason, Guid closedEnrolmentId, Guid? newEnrolmentId)
        : base(id)
    {
        BatchId = batchId;
        PupilId = pupilId;
        FromArmId = fromArmId;
        ProposedOutcome = proposedOutcome;
        Outcome = outcome;
        TargetArmId = targetArmId;
        Reason = reason;
        ClosedEnrolmentId = closedEnrolmentId;
        NewEnrolmentId = newEnrolmentId;
    }

    // EF Core materialisation constructor.
    private PromotionDecision()
        : base()
    {
    }

    /// <summary>The batch.</summary>
    public Guid BatchId { get; private set; }

    /// <summary>The pupil.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>The arm the pupil ended the old session in.</summary>
    public Guid FromArmId { get; private set; }

    /// <summary>What the system proposed, or null when there was no annual result to propose from.</summary>
    public PromotionDecisionOutcome? ProposedOutcome { get; private set; }

    /// <summary>What was committed.</summary>
    public PromotionDecisionOutcome Outcome { get; private set; }

    /// <summary>The new arm; null for a graduate.</summary>
    public Guid? TargetArmId { get; private set; }

    /// <summary>Required for promoted on trial.</summary>
    public string? Reason { get; private set; }

    /// <summary>The old enrolment, closed at the old session's end; reversal reopens it.</summary>
    public Guid ClosedEnrolmentId { get; private set; }

    /// <summary>The new enrolment; reversal removes it. Null for a graduate.</summary>
    public Guid? NewEnrolmentId { get; private set; }

    /// <summary>A decision, trusted: the handler has validated the outcome, the arm and the reason against the rules.</summary>
    public static PromotionDecision Create(
        Guid id, Guid batchId, Guid pupilId, Guid fromArmId, PromotionDecisionOutcome? proposedOutcome, PromotionDecisionOutcome outcome,
        Guid? targetArmId, string? reason, Guid closedEnrolmentId, Guid? newEnrolmentId) =>
        new(id, batchId, pupilId, fromArmId, proposedOutcome, outcome, targetArmId, reason, closedEnrolmentId, newEnrolmentId);
}
