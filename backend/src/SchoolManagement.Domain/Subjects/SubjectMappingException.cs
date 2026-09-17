using System.Diagnostics.CodeAnalysis;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Subjects;

/// <summary>
/// A per-arm exception to a level's subject mapping for one term (spec 6.6.4): <c>Include</c> adds a
/// subject the level does not take, <c>Exclude</c> removes one the level does take.
/// </summary>
/// <remarks>
/// Unique on <c>(arm_id, subject_id, term_id)</c> regardless of <see cref="Mode"/> — a single row can
/// exist for a given triple, so an include and an exclude on the same triple can never coexist, and
/// this row is DELETED (never "ended") to withdraw it; there is no <c>status</c> column.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "'subject_mapping_exception' is spec 6.6.4's own entity name (an exception to a " +
                    "level mapping, not a .NET exception type) — the same reasoning Domain.Common.Error " +
                    "already gives for CA1716 on 'Error'. This type never derives from System.Exception " +
                    "and is never thrown.")]
public sealed class SubjectMappingException : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.6.4: <c>reason</c> is <c>String 200</c>.</summary>
    public const int ReasonMaxLength = 200;

    private SubjectMappingException(Guid id, Guid armId, Guid subjectId, Guid termId, SubjectExceptionMode mode, string reason)
        : base(id)
    {
        ArmId = armId;
        SubjectId = subjectId;
        TermId = termId;
        Mode = mode;
        Reason = reason;
    }

    // EF Core materialisation constructor.
    private SubjectMappingException()
        : base() => Reason = null!;

    /// <summary>The one arm this exception applies to (spec 6.6.4).</summary>
    public Guid ArmId { get; private set; }

    /// <summary>Must reference an active subject (spec 6.6.4). The caller checks this.</summary>
    public Guid SubjectId { get; private set; }

    /// <summary>Must match the arm's session (spec 6.6.4). The caller checks this.</summary>
    public Guid TermId { get; private set; }

    /// <summary>Include or exclude (spec 6.6.4).</summary>
    public SubjectExceptionMode Mode { get; private set; }

    /// <summary>
    /// Required — "an exception without a stated reason becomes a mystery within one term" (spec
    /// 6.6.4).
    /// </summary>
    public string Reason { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a new exception. The caller must already have checked arm/subject/term existence and
    /// state, that the redundancy rule (spec 6.6.4) does not apply, and active-uniqueness on
    /// <c>(arm_id, subject_id, term_id)</c> — none of those lookups is available to this entity.
    /// </summary>
    public static Result<SubjectMappingException> Create(
        Guid id, Guid armId, Guid subjectId, Guid termId, SubjectExceptionMode mode, string reason)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<SubjectMappingException>(Error.Validation(
                "subject_exception.id_required", "Id must not be empty."));
        }

        if (armId == Guid.Empty || subjectId == Guid.Empty || termId == Guid.Empty)
        {
            return Result.Failure<SubjectMappingException>(Error.Validation(
                "subject_exception.reference_required", "ArmId, SubjectId and TermId must not be empty."));
        }

        if (!TryNormalizeReason(reason, out var trimmedReason, out var reasonError))
        {
            return Result.Failure<SubjectMappingException>(reasonError);
        }

        return Result.Success(new SubjectMappingException(id, armId, subjectId, termId, mode, trimmedReason));
    }

    private static bool TryNormalizeReason(string reason, out string normalized, out Error error)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var trimmed = reason.Trim();

        if (trimmed.Length == 0 || trimmed.Length > ReasonMaxLength)
        {
            normalized = string.Empty;
            error = Error.Validation(
                "subject_exception.reason_invalid_length", $"Reason must be 1 to {ReasonMaxLength} characters.");
            return false;
        }

        normalized = trimmed;
        error = Error.None;
        return true;
    }
}
