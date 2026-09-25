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
    /// <summary>Spec 6.7.8's floor for a return reason, trimmed, before it is rejected 422 (TASK-0090).</summary>
    public const int ReturnReasonMinLength = 10;

    /// <summary>Spec 6.7.3's <see cref="ReturnReason"/> column width, trimmed (TASK-0090).</summary>
    public const int ReturnReasonMaxLength = 500;

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

    /// <summary>
    /// System-triggered flag (TASK-0088 AC A1/A2): a §6.2.9 settings save or a subject-mapping change
    /// invalidates this set's computed rows — spec 6.7.11's "Any state -&gt; Same state with
    /// needs_recompute true | System" row. Distinct from <see cref="MarkNeedsRecompute"/>, which the
    /// six sheet-save handlers call on a plain mark/rating/attendance/remark edit and which NEVER
    /// changes <see cref="State"/> — only a settings or mapping change can drop a Returned for
    /// Correction set back to Draft (the state machine's separate System row, same table), because
    /// Awaiting Approval and Approved must keep their state and only gain the flag (human ruling,
    /// TASK-0088 carding).
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when <see cref="State"/> actually changed (Returned for Correction to
    /// Draft), so the caller knows whether this is a genuine state transition worth its own audit
    /// event, as opposed to a set that merely gained the flag.
    /// </returns>
    public bool FlagNeedsRecomputeBySystem()
    {
        NeedsRecompute = true;

        if (State != ResultSetState.ReturnedForCorrection)
        {
            return false;
        }

        State = ResultSetState.Draft;
        return true;
    }

    /// <summary>
    /// A pupil moved into or out of this set's arm (spec 06 §6.4.4 step 5, 09 §6.7.12): the cohort changed, so class
    /// averages and positions are stale. Flags the set; Awaiting Approval drops to Draft with a system note, so the head
    /// teacher sees why the item left the approval queue, and Returned for Correction drops to Draft as it does for a
    /// settings change (keeping the head teacher's reason). Approved keeps its state and only gains the flag, which
    /// publication already refuses. The caller never passes a Published set: it renders from its snapshot.
    /// </summary>
    /// <param name="note">The system-written note, for example "Cohort changed by pupil transfer on 14/10/2026".</param>
    /// <returns><see langword="true"/> when <see cref="State"/> changed.</returns>
    public bool FlagCohortChanged(string note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        if (State == ResultSetState.AwaitingApproval)
        {
            NeedsRecompute = true;
            State = ResultSetState.Draft;
            ReturnReason = note.Length <= ReturnReasonMaxLength ? note : note[..ReturnReasonMaxLength];
            return true;
        }

        return FlagNeedsRecomputeBySystem();
    }

    /// <summary>
    /// Applies computation's outcome (spec 8.2 step 11): stamps <see cref="ComputedAtUtc"/>/
    /// <see cref="ComputedBy"/>, writes <see cref="PupilCount"/> (the ranked denominator printed on
    /// the sheet), and clears <see cref="NeedsRecompute"/>. Does NOT change <see cref="State"/> —
    /// computation is a same-state transition in Draft, Awaiting Approval, Approved and Returned for
    /// Correction alike (spec 6.7.11 row 2; TASK-0071's contract delta).
    /// </summary>
    public void MarkComputed(Guid? computedBy, DateTimeOffset computedAtUtc, int pupilCount)
    {
        ComputedAtUtc = computedAtUtc;
        ComputedBy = computedBy;
        PupilCount = pupilCount;
        NeedsRecompute = false;
    }

    /// <summary>
    /// Moves Draft or Returned for Correction to Awaiting Approval (spec 6.7.11; TASK-0088 stage B) —
    /// covers both the first submission and a resubmission, which is why <see cref="ReturnReason"/> is
    /// unconditionally cleared here rather than only when it was set. The caller has already checked
    /// <see cref="State"/> and the completeness gate; this entity trusts that, the same posture
    /// <see cref="MarkComputed"/> takes.
    /// </summary>
    public void Submit(Guid? submittedBy, DateTimeOffset submittedAtUtc)
    {
        State = ResultSetState.AwaitingApproval;
        SubmittedAtUtc = submittedAtUtc;
        SubmittedBy = submittedBy;
        ReturnReason = null;
    }

    /// <summary>
    /// Moves Awaiting Approval to Approved (spec 6.7.8, 6.7.11; TASK-0090). The caller has already
    /// checked <see cref="State"/> and <see cref="NeedsRecompute"/> — this entity trusts that, the
    /// same posture <see cref="Submit"/> takes. Returns <see cref="Result"/>, not <see langword="void"/>
    /// (card AC), so a future invariant can fail here without a signature change.
    /// </summary>
    public Result Approve(Guid? approvedBy, DateTimeOffset approvedAtUtc)
    {
        State = ResultSetState.Approved;
        ApprovedAtUtc = approvedAtUtc;
        ApprovedBy = approvedBy;
        return Result.Success();
    }

    /// <summary>
    /// Moves Awaiting Approval or Approved to Returned for Correction (spec 6.7.8, 6.7.11; TASK-0090),
    /// storing the head teacher's reason and reopening marks for the class teacher. A set can be
    /// returned any number of times: a second return overwrites the reason, which is why this is
    /// unconditional rather than guarded on the reason already being empty — the caller records each
    /// call as its own audit event (spec 6.7.8: "each return is a separate audit event"). The caller
    /// has already checked <see cref="State"/> and validated <paramref name="reason"/>'s bounds.
    /// Returns <see cref="Result"/>, not <see langword="void"/> (card AC), the same reason as
    /// <see cref="Approve"/>.
    /// </summary>
    public Result Return(string reason)
    {
        State = ResultSetState.ReturnedForCorrection;
        ReturnReason = reason;
        return Result.Success();
    }

    /// <summary>
    /// Moves Approved to Published (spec 6.7.9). It writes the configuration snapshot and bumps
    /// <see cref="RevisionNumber"/> (1 on first publication, 2 after a withdrawal and reopen). The caller has
    /// already checked every §6.7.9 precondition and records a <see cref="ResultSetSnapshot"/> history row, so
    /// <see cref="ConfigSnapshotJson"/> holds the current revision while every earlier one survives.
    /// </summary>
    public Result Publish(Guid? publishedBy, DateTimeOffset publishedAtUtc, string configSnapshotJson, Guid? configVersionId)
    {
        if (State != ResultSetState.Approved)
        {
            return Result.Failure(Error.Conflict("result_set.not_approved", $"This result set is {State} and cannot be published."));
        }

        State = ResultSetState.Published;
        PublishedAtUtc = publishedAtUtc;
        PublishedBy = publishedBy;
        RevisionNumber++;
        ConfigSnapshotJson = configSnapshotJson;
        ConfigVersionId = configVersionId;
        return Result.Success();
    }

    /// <summary>
    /// Moves Published to Withdrawn (spec 6.7.9), which takes it off the parent portal at once. The snapshot and
    /// its history row stay. The caller has validated the reason and records it on the audit event.
    /// </summary>
    public Result Withdraw()
    {
        if (State != ResultSetState.Published)
        {
            return Result.Failure(Error.Conflict("result_set.not_published", $"This result set is {State} and cannot be withdrawn."));
        }

        State = ResultSetState.Withdrawn;
        return Result.Success();
    }

    /// <summary>
    /// Moves Withdrawn to Draft for correction (spec 6.7.9, 6.7.11). Marks become editable again and computation
    /// must re-run, so <see cref="NeedsRecompute"/> is set. The caller checks the term is active.
    /// </summary>
    public Result Reopen()
    {
        if (State != ResultSetState.Withdrawn)
        {
            return Result.Failure(Error.Conflict("result_set.not_withdrawn", $"This result set is {State} and cannot be reopened."));
        }

        State = ResultSetState.Draft;
        NeedsRecompute = true;
        return Result.Success();
    }
}
