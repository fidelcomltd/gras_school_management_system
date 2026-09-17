using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Results;

/// <summary>
/// One pupil's marks for one subject in one term (spec 09 §6.7.3) — the row a class teacher's score
/// sheet writes, one per pupil per subject per term.
/// </summary>
/// <remarks>
/// <para>
/// UNIQUE ON <c>(pupil_id, subject_id, term_id)</c> <b>WHERE voided_at IS NULL</b> — a partial index
/// (see the Infrastructure configuration), the same shape as <c>SubjectMapping</c>'s active-only
/// uniqueness and <c>Enrolment</c>'s one-open-row invariant. Without the filter, re-entering marks
/// after <see cref="Void"/> would collide with the voided row it is meant to replace.
/// </para>
/// <para>
/// <see cref="ComponentMarksJson"/> is stored as raw JSON text (jsonb) — a map from assessment
/// component id to an integer mark, the same "typed string, caller (de)serialises" convention as
/// <c>AuditEvent.BeforeJson</c>/<c>AfterJson</c>. NOTHING HERE ASSUMES A COMPONENT COUNT (TASK-0069's
/// durability rule) — the shape is opaque to this entity and to the database.
/// </para>
/// <para>
/// Spec's <c>entered_by</c>/<c>entered_at</c> are NOT separate columns here — they are
/// <see cref="IAuditableEntity.ModifiedBy"/>/<see cref="IAuditableEntity.ModifiedAtUtc"/> once a row
/// has been edited, else <see cref="IAuditableEntity.CreatedBy"/>/<see cref="IAuditableEntity.CreatedAtUtc"/>,
/// "the account that last wrote the row" being exactly what those already mean.
/// </para>
/// <para>
/// TASK-0076 dispatch A builds <see cref="Create"/> and <see cref="Void"/> only — the score-sheet
/// save/void endpoints that call them, and every per-cell validation rule (spec 6.7.4), are dispatch
/// B's job; this entity trusts its caller the same way <c>SubjectMapping.Create</c> does.
/// </para>
/// </remarks>
public sealed class SubjectScore : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.7.4/6.7.11's reason floor, reused here for <see cref="VoidReason"/>'s minimum.</summary>
    public const int VoidReasonMinLength = 10;

    /// <summary>Spec 6.7.3: <c>void_reason</c>, <c>String 500</c>.</summary>
    public const int VoidReasonMaxLength = 500;

    private SubjectScore(
        Guid id,
        Guid resultSetId,
        Guid pupilId,
        Guid subjectId,
        Guid termId,
        string componentMarksJson,
        int? examMark,
        bool examAbsent)
        : base(id)
    {
        ResultSetId = resultSetId;
        PupilId = pupilId;
        SubjectId = subjectId;
        TermId = termId;
        ComponentMarksJson = componentMarksJson;
        ExamMark = examMark;
        ExamAbsent = examAbsent;
    }

    // EF Core materialisation constructor.
    private SubjectScore()
        : base()
    {
        ComponentMarksJson = null!;
    }

    /// <summary>The result set this mark belongs to (spec: "denormalised for query speed").</summary>
    public Guid ResultSetId { get; private set; }

    /// <summary>Unique together with <see cref="SubjectId"/> and <see cref="TermId"/>, while not voided.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>Must be in the set of subjects in effect for the arm this term (the caller checks this via <c>SubjectsInEffectResolver</c>).</summary>
    public Guid SubjectId { get; private set; }

    /// <summary>Marks are always term-bound (spec 6.7.3).</summary>
    public Guid TermId { get; private set; }

    /// <summary>
    /// A JSON object mapping assessment component id to an integer mark, for the non-examination
    /// components. Raw text (jsonb) — see the type remarks.
    /// </summary>
    public string ComponentMarksJson { get; private set; }

    /// <summary>Required unless <see cref="ExamAbsent"/>. 0 to the examination maximum from settings.</summary>
    public int? ExamMark { get; private set; }

    /// <summary>True means the pupil did not sit the examination. Defaults false.</summary>
    public bool ExamAbsent { get; private set; }

    /// <summary>Set once, by <see cref="Void"/>. <see langword="null"/> while the row counts toward computation.</summary>
    public DateTimeOffset? VoidedAt { get; private set; }

    /// <summary>Set once, by <see cref="Void"/>.</summary>
    public string? VoidedBy { get; private set; }

    /// <summary>
    /// Set once, by <see cref="Void"/>. <see cref="VoidReasonMinLength"/> to
    /// <see cref="VoidReasonMaxLength"/> characters — the caller validates length; this entity only
    /// rejects a blank one.
    /// </summary>
    public string? VoidReason { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a new mark row. The caller (dispatch B's save handler) has already validated every
    /// per-cell rule (spec 6.7.4) — this entity trusts its input, the same posture
    /// <c>SubjectMapping.Create</c> takes.
    /// </summary>
    public static Result<SubjectScore> Create(
        Guid id,
        Guid resultSetId,
        Guid pupilId,
        Guid subjectId,
        Guid termId,
        string componentMarksJson,
        int? examMark,
        bool examAbsent)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<SubjectScore>(Error.Validation("subject_score.id_required", "Id must not be empty."));
        }

        if (resultSetId == Guid.Empty || pupilId == Guid.Empty || subjectId == Guid.Empty || termId == Guid.Empty)
        {
            return Result.Failure<SubjectScore>(Error.Validation(
                "subject_score.reference_required",
                "ResultSetId, PupilId, SubjectId and TermId must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(componentMarksJson))
        {
            return Result.Failure<SubjectScore>(Error.Validation(
                "subject_score.component_marks_required", "ComponentMarksJson must not be empty."));
        }

        return Result.Success(new SubjectScore(id, resultSetId, pupilId, subjectId, termId, componentMarksJson, examMark, examAbsent));
    }

    /// <summary>
    /// Applies a new set of marks to an EXISTING row (TASK-0076 dispatch B's score-sheet save). The
    /// caller (<c>SaveScoreSheetHandler</c>) has already validated every per-cell rule (spec 6.7.4) —
    /// this entity trusts its input, the same posture <see cref="Create"/> takes. Guarded against a
    /// voided row the same way <see cref="Void"/> guards against a double void: a voided row is
    /// excluded from every active-only query this handler reads from, so reaching this method on one
    /// would mean the caller loaded it some other way.
    /// </summary>
    public Result UpdateMarks(string componentMarksJson, int? examMark, bool examAbsent)
    {
        if (VoidedAt is not null)
        {
            return Result.Failure(Error.Conflict(
                "subject_score.already_voided", "This mark has already been voided and cannot be edited."));
        }

        if (string.IsNullOrWhiteSpace(componentMarksJson))
        {
            return Result.Failure(Error.Validation(
                "subject_score.component_marks_required", "ComponentMarksJson must not be empty."));
        }

        ComponentMarksJson = componentMarksJson;
        ExamMark = examMark;
        ExamAbsent = examAbsent;
        return Result.Success();
    }

    /// <summary>
    /// Voids this row (spec 6.7.4, 6.7.11): excluded from computation, retained. Guarded — a row
    /// cannot be voided twice. The caller has already validated the reason's length.
    /// </summary>
    public Result Void(string reason, string? voidedBy, DateTimeOffset voidedAt)
    {
        if (VoidedAt is not null)
        {
            return Result.Failure(Error.Conflict(
                "subject_score.already_voided", "This mark has already been voided."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("subject_score.void_reason_required", "A reason is required to void a mark."));
        }

        VoidedAt = voidedAt;
        VoidedBy = voidedBy;
        VoidReason = reason;
        return Result.Success();
    }
}
