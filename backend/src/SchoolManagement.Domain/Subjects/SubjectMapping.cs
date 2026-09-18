using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Subjects;

/// <summary>
/// Attaches a <see cref="Subject"/> to a class level for one session and one term (spec 6.6.3).
/// Every arm under that level inherits it. Per-term rather than per-session so a subject introduced
/// in Second Term does not appear as an empty row on the First Term sheet, and a subject dropped
/// after First Term leaves that term's published result intact.
/// </summary>
/// <remarks>
/// Unique on <c>(subject_id, class_level_id, term_id)</c> WHERE <see cref="Status"/> is
/// <see cref="SubjectMappingStatus.Active"/> — a partial index, same shape as
/// <c>ClassLevelConfiguration</c>'s partial index on <c>progression_order</c> and the one-open-row
/// invariant on <c>enrolment</c> (TASK-0059).
/// </remarks>
public sealed class SubjectMapping : Entity<Guid>, IAuditableEntity
{
    private SubjectMapping(Guid id, Guid subjectId, Guid classLevelId, Guid sessionId, Guid termId, int displayOrder)
        : base(id)
    {
        SubjectId = subjectId;
        ClassLevelId = classLevelId;
        SessionId = sessionId;
        TermId = termId;
        DisplayOrder = displayOrder;
        Status = SubjectMappingStatus.Active;
    }

    // EF Core materialisation constructor.
    private SubjectMapping()
        : base()
    {
    }

    /// <summary>Must reference an active subject at creation (spec 6.6.3). The caller checks this.</summary>
    public Guid SubjectId { get; private set; }

    /// <summary>Must reference an active level (spec 6.6.3). The caller checks this.</summary>
    public Guid ClassLevelId { get; private set; }

    /// <summary>Must be upcoming or active (spec 6.6.3). The caller checks this.</summary>
    public Guid SessionId { get; private set; }

    /// <summary>Must belong to <see cref="SessionId"/> (spec 6.6.3). The caller checks this.</summary>
    public Guid TermId { get; private set; }

    /// <summary>
    /// Row order of the subject on the result sheet for this level (spec 6.6.3) — a property of the
    /// MAPPING, not of the subject, because one subject can sit at a different row on each section's
    /// sheet. Drag-ordered.
    /// </summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Active or ended (spec 6.6.3). Defaults active.</summary>
    public SubjectMappingStatus Status { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a new, active mapping. The caller must already have checked subject/level/session/term
    /// existence and state, and active-uniqueness on <c>(subject_id, class_level_id, term_id)</c> —
    /// none of those lookups is available to this entity.
    /// </summary>
    public static Result<SubjectMapping> Create(
        Guid id, Guid subjectId, Guid classLevelId, Guid sessionId, Guid termId, int displayOrder)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<SubjectMapping>(Error.Validation(
                "subject_mapping.id_required", "Id must not be empty."));
        }

        if (subjectId == Guid.Empty || classLevelId == Guid.Empty || sessionId == Guid.Empty || termId == Guid.Empty)
        {
            return Result.Failure<SubjectMapping>(Error.Validation(
                "subject_mapping.reference_required", "SubjectId, ClassLevelId, SessionId and TermId must not be empty."));
        }

        return Result.Success(new SubjectMapping(id, subjectId, classLevelId, sessionId, termId, displayOrder));
    }

    /// <summary>Changes the row order (spec 6.6.5's drag-ordering). No other field is editable in place.</summary>
    public void ChangeDisplayOrder(int displayOrder) => DisplayOrder = displayOrder;

    /// <summary>
    /// Ends the mapping (spec 6.6.5, 6.6.6). The caller must already have checked the term is not
    /// closed and that no <c>subject_score</c> exists for this subject in any arm under the level in
    /// the active term — this entity cannot perform either check.
    /// </summary>
    public void End() => Status = SubjectMappingStatus.Ended;
}
