using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Results;

/// <summary>
/// One arm's result set for one term (spec 09 §6.7.1, §6.7.3) — the unit of work the class teacher
/// enters marks into, the head teacher approves, and that becomes visible to parents once published.
/// </summary>
/// <remarks>
/// <para>
/// UNIQUE ON <c>(arm_id, term_id)</c> — one result set per arm per term, enforced by a database
/// unique index (see the Infrastructure configuration), not here: this entity cannot see a sibling
/// row for the same arm and term.
/// </para>
/// <para>
/// TASK-0076 dispatch A builds ONLY <see cref="Create"/> — every state transition (submit, approve,
/// return, publish, withdraw, reopen, computation) belongs to a later card's endpoint per spec
/// §6.7.11's state machine. <see cref="Create"/> itself is dispatch B's "first save" transition
/// (Not started -&gt; Draft); this card wires no caller to it yet.
/// </para>
/// </remarks>
public sealed class ResultSet : Entity<Guid>, IAuditableEntity
{
    private ResultSet(Guid id, Guid armId, Guid termId)
        : base(id)
    {
        ArmId = armId;
        TermId = termId;
        State = ResultSetState.Draft;
        NeedsRecompute = true;
        RevisionNumber = 0;
    }

    // EF Core materialisation constructor.
    private ResultSet()
        : base()
    {
    }

    /// <summary>The arm this result set belongs to. Unique together with <see cref="TermId"/>.</summary>
    public Guid ArmId { get; private set; }

    /// <summary>Must belong to the arm's session (spec 6.7.3). The caller checks this.</summary>
    public Guid TermId { get; private set; }

    /// <summary>Spec 6.7.11's six-state machine. "Not started" is the absence of a row, not a member.</summary>
    public ResultSetState State { get; private set; }

    /// <summary>
    /// Set by any event that invalidates the computed rows (spec 6.7.3): a mark change, a pupil
    /// transfer in or out, a subject mapping change, or a settings change under 6.2.9. Cleared by a
    /// successful computation. Only the mark-change trigger is wired by TASK-0076 — see the drift
    /// entry recorded at this card's close for the remaining triggers.
    /// </summary>
    public bool NeedsRecompute { get; private set; }

    /// <summary>Written by computation (a later card). <see langword="null"/> until then.</summary>
    public DateTimeOffset? ComputedAtUtc { get; private set; }

    /// <summary>Written by computation (a later card). <see langword="null"/> until then.</summary>
    public Guid? ComputedBy { get; private set; }

    /// <summary>Written on submission (a later card). <see langword="null"/> until then.</summary>
    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    /// <summary>Written on submission (a later card). <see langword="null"/> until then.</summary>
    public Guid? SubmittedBy { get; private set; }

    /// <summary>Written on approval (a later card). <see langword="null"/> until then.</summary>
    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    /// <summary>Written on approval (a later card). <see langword="null"/> until then.</summary>
    public Guid? ApprovedBy { get; private set; }

    /// <summary>Written on publication (a later card). <see langword="null"/> until then.</summary>
    public DateTimeOffset? PublishedAtUtc { get; private set; }

    /// <summary>Written on publication (a later card). <see langword="null"/> until then.</summary>
    public Guid? PublishedBy { get; private set; }

    /// <summary>0 until first publication, then 1, incremented on each republication after a withdrawal.</summary>
    public int RevisionNumber { get; private set; }

    /// <summary>Set when returned for correction. Cleared on the next submission. Spec: String 500.</summary>
    public string? ReturnReason { get; private set; }

    /// <summary>
    /// The whole configuration as of first publication, written once and never overwritten (spec
    /// 6.7.3) — a republication writes a NEW snapshot row and this points at the current one, so the
    /// history of what each revision looked like survives. Raw JSON text (jsonb), same convention as
    /// <c>ConfigVersion.SnapshotJson</c>.
    /// </summary>
    public string? ConfigSnapshotJson { get; private set; }

    /// <summary>Written with the snapshot (a later card). <see langword="null"/> until then.</summary>
    public Guid? ConfigVersionId { get; private set; }

    /// <summary>Number of pupils ranked, written by computation (a later card). <see langword="null"/> until then.</summary>
    public int? PupilCount { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Opens a result set in <see cref="ResultSetState.Draft"/> with <see cref="NeedsRecompute"/> true
    /// (spec 6.7.11: "First save of any mark, trait, attendance or remark for the arm and term...
    /// creates the result set with needs_recompute true"). The caller must already have checked no
    /// result set exists yet for <paramref name="armId"/>/<paramref name="termId"/> — this entity
    /// cannot see a sibling row; the database's unique index is the real backstop.
    /// </summary>
    public static Result<ResultSet> Create(Guid id, Guid armId, Guid termId)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<ResultSet>(Error.Validation("result_set.id_required", "Id must not be empty."));
        }

        if (armId == Guid.Empty || termId == Guid.Empty)
        {
            return Result.Failure<ResultSet>(Error.Validation(
                "result_set.reference_required", "ArmId and TermId must not be empty."));
        }

        return Result.Success(new ResultSet(id, armId, termId));
    }

    /// <summary>
    /// Flags the computed rows as stale (spec 6.7.3, 6.7.11: "Any state with needs_recompute true...
    /// Triggered by a mark edit"). TASK-0076 dispatch B calls this on every successful score-sheet
    /// save, whether the set was just created (already true from <see cref="Create"/>, so this is a
    /// harmless no-op restamp) or already existed. Idempotent and unconditional — no other trigger
    /// (pupil transfer, mapping change, settings change) is wired yet; see the drift entry recorded
    /// at this card's close.
    /// </summary>
    public void MarkNeedsRecompute() => NeedsRecompute = true;
}
