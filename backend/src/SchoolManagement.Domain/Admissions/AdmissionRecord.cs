using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Admissions;

/// <summary>
/// Sections A, I and J of the paper admission form (spec 6.5.9): one row per <c>pupil</c>, created in
/// the SAME transaction as the pupil record (<c>CreatePupilHandler</c>) so a pupil with no admission
/// record is a state the code cannot produce.
/// </summary>
/// <remarks>
/// <para>
/// Steps 2 to 8 of the admission flow (spec 6.5.11) — contacts, health, barred persons, pickup
/// persons, documents — have no entity yet (TASK-0062's own out-of-scope list). This type therefore
/// carries only what sections A, I and J name; nothing here models the resumability of those other
/// steps.
/// </para>
/// <para>
/// <see cref="ApprovedBy"/> and <see cref="ApprovedAt"/> are section J fields this type exposes but
/// never writes — they are written only by admission approval (TASK-0051), which does not exist yet.
/// </para>
/// </remarks>
public sealed class AdmissionRecord : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.5.9: <c>admission_type_note</c> is <c>String 120</c>.</summary>
    public const int AdmissionTypeNoteMaxLength = 120;

    /// <summary>Spec 6.5.9: <c>assessment_result_remarks</c> is <c>Text 500</c>.</summary>
    public const int AssessmentResultRemarksMaxLength = 500;

    /// <summary>Spec 6.5.9: <c>declaration_name</c> is <c>String 120</c>.</summary>
    public const int DeclarationNameMaxLength = 120;

    /// <summary>Spec 6.5.9: <c>head_of_school_name</c> is <c>String 120</c>.</summary>
    public const int HeadOfSchoolNameMaxLength = 120;

    /// <summary>Spec 6.5.16's override reason, 10 to 500 characters (human ruling 2026-09-23).</summary>
    public const int HealthOverrideReasonMaxLength = 500;

    private AdmissionRecord(
        Guid id,
        Guid pupilId,
        Guid sessionId,
        DateOnly? dateApplicationReceived,
        DateOnly dateAdmitted,
        Guid classAdmittedInto,
        AdmissionType admissionType,
        string? admissionTypeNote,
        bool assessmentRequired)
        : base(id)
    {
        PupilId = pupilId;
        SessionId = sessionId;
        DateApplicationReceived = dateApplicationReceived;
        DateAdmitted = dateAdmitted;
        ClassAdmittedInto = classAdmittedInto;
        AdmissionType = admissionType;
        AdmissionTypeNote = admissionTypeNote;
        AssessmentRequired = assessmentRequired;
        DeclarationSigned = false;
        HeadOfSchoolConfirmed = false;
    }

    // EF Core materialisation constructor.
    private AdmissionRecord()
        : base()
    {
    }

    /// <summary>The pupil this record belongs to. Never changes once set.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>The session admitted into. Defaults to the active session at creation (spec 6.5.9).</summary>
    public Guid SessionId { get; private set; }

    /// <summary>Optional. Not in the future.</summary>
    public DateOnly? DateApplicationReceived { get; private set; }

    /// <summary>Not in the future. The registration number's year comes from this (spec 6.5.10).</summary>
    public DateOnly DateAdmitted { get; private set; }

    /// <summary>
    /// A <c>ClassLevel</c> id. The caller (the command handler) has already checked it references an
    /// existing, active level — this entity cannot perform that lookup. Whether an arm exists for it
    /// in the session is an admission-APPROVAL concern (spec 6.5.11), never checked here.
    /// </summary>
    public Guid ClassAdmittedInto { get; private set; }

    /// <summary>The form's New / Returning tick.</summary>
    public AdmissionType AdmissionType { get; private set; }

    /// <summary>The blank line beside the New/Returning tick. Optional.</summary>
    public string? AdmissionTypeNote { get; private set; }

    /// <summary>Explicit yes or no — whether an entrance assessment is required.</summary>
    public bool AssessmentRequired { get; private set; }

    /// <summary>
    /// The assessment's outcome. Optional to SAVE — see <see cref="EnsureAssessmentResultRecordedIfRequired"/>
    /// for the separate, stricter check TASK-0051's approval handler runs.
    /// </summary>
    public string? AssessmentResultRemarks { get; private set; }

    /// <summary>An admin account id. Informational only at this stage (spec 6.5.9) — never validated against the admin roster here.</summary>
    public Guid? AssignedClassTeacher { get; private set; }

    /// <summary>Section I: the declaring parent's name. Required for approval, not for saving.</summary>
    public string? DeclarationName { get; private set; }

    /// <summary>Section I: whether the signed paper form exists. Defaults false.</summary>
    public bool DeclarationSigned { get; private set; }

    /// <summary>
    /// Section I: when the form was signed. Kept consistent with <see cref="DeclarationSigned"/> by
    /// <see cref="Update"/> — never non-null while <see cref="DeclarationSigned"/> is false.
    /// </summary>
    public DateOnly? DeclarationDate { get; private set; }

    /// <summary>Section J. Written by admission approval only (TASK-0051) — always <see langword="null"/> within this card.</summary>
    public Guid? ApprovedBy { get; private set; }

    /// <summary>Section J. Written by admission approval only (TASK-0051) — always <see langword="null"/> within this card.</summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>
    /// Spec 6.5.16: why the admission was approved with the health questions unanswered. Held here, never in audit metadata,
    /// because it may describe the child's health; read it with the safeguarding privilege, as the health block is.
    /// </summary>
    public string? HealthOverrideReason { get; private set; }

    /// <summary>Section J's second signature block. Defaults false.</summary>
    public bool HeadOfSchoolConfirmed { get; private set; }

    /// <summary>Section J. Optional; no settings-derived default is applied by this type.</summary>
    public string? HeadOfSchoolName { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates section A of a new admission record. The caller has already resolved
    /// <paramref name="sessionId"/> (defaulting to the active session) and checked
    /// <paramref name="classAdmittedInto"/> references an existing, active <c>ClassLevel</c> — this
    /// entity cannot perform either lookup.
    /// </summary>
    /// <param name="id">A fresh <see cref="Guid.CreateVersion7()"/> value.</param>
    /// <param name="pupilId">The pupil this record belongs to.</param>
    /// <param name="sessionId">The session admitted into.</param>
    /// <param name="dateApplicationReceived">Optional; not in the future.</param>
    /// <param name="dateAdmitted">Not in the future. <see langword="null"/> defaults to <paramref name="asOfDate"/>.</param>
    /// <param name="asOfDate">"Today" — never computed internally; see <c>TimeProvider</c>.</param>
    /// <param name="classAdmittedInto">Must reference an existing, active class level (checked by the caller).</param>
    /// <param name="admissionType">New or returning.</param>
    /// <param name="admissionTypeNote">Optional, at most <see cref="AdmissionTypeNoteMaxLength"/> characters.</param>
    /// <param name="assessmentRequired">Explicit yes or no.</param>
    public static Result<AdmissionRecord> Create(
        Guid id,
        Guid pupilId,
        Guid sessionId,
        DateOnly? dateApplicationReceived,
        DateOnly? dateAdmitted,
        DateOnly asOfDate,
        Guid classAdmittedInto,
        AdmissionType admissionType,
        string? admissionTypeNote,
        bool assessmentRequired)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<AdmissionRecord>(Error.Validation("admission_record.id_required", "Id must not be empty."));
        }

        if (pupilId == Guid.Empty)
        {
            return Result.Failure<AdmissionRecord>(Error.Validation("admission_record.pupil_id_required", "PupilId must not be empty."));
        }

        if (sessionId == Guid.Empty)
        {
            return Result.Failure<AdmissionRecord>(Error.Validation("admission_record.session_id_required", "SessionId must not be empty."));
        }

        if (classAdmittedInto == Guid.Empty)
        {
            return Result.Failure<AdmissionRecord>(Error.Validation(
                "admission_record.class_admitted_into_required", "ClassAdmittedInto must not be empty."));
        }

        if (dateApplicationReceived is { } receivedDate && receivedDate > asOfDate)
        {
            return Result.Failure<AdmissionRecord>(DateInFutureError(
                "admission_record.date_application_received_in_future", "Date application received", receivedDate));
        }

        var resolvedDateAdmitted = dateAdmitted ?? asOfDate;

        if (resolvedDateAdmitted > asOfDate)
        {
            return Result.Failure<AdmissionRecord>(DateInFutureError(
                "admission_record.date_admitted_in_future", "Date admitted", resolvedDateAdmitted));
        }

        if (!TryNormalizeOptional(admissionTypeNote, currentValue: null, AdmissionTypeNoteMaxLength, "AdmissionTypeNote", out var normalizedNote, out var noteError))
        {
            return Result.Failure<AdmissionRecord>(noteError);
        }

        return Result.Success(new AdmissionRecord(
            id,
            pupilId,
            sessionId,
            dateApplicationReceived,
            resolvedDateAdmitted,
            classAdmittedInto,
            admissionType,
            normalizedNote,
            assessmentRequired));
    }

    /// <summary>
    /// Applies a partial payload (spec 6.5.11: "a half-finished step still saves"). Every parameter is
    /// independently optional — <see langword="null"/> leaves the field unchanged, the same convention
    /// <c>UpdateArmCommand</c>/<c>UpdatePupilBiographicalCommand</c> established; an empty string
    /// clears an optional string field. The caller has already checked <paramref name="sessionId"/>
    /// and <paramref name="classAdmittedInto"/>, when supplied, reference real rows.
    /// </summary>
    /// <param name="sessionId"><see langword="null"/> leaves it unchanged.</param>
    /// <param name="dateApplicationReceived"><see langword="null"/> leaves it unchanged; not in the future.</param>
    /// <param name="dateAdmitted"><see langword="null"/> leaves it unchanged; not in the future.</param>
    /// <param name="classAdmittedInto"><see langword="null"/> leaves it unchanged.</param>
    /// <param name="admissionType"><see langword="null"/> leaves it unchanged.</param>
    /// <param name="admissionTypeNote"><see langword="null"/> leaves it unchanged; an empty string clears it.</param>
    /// <param name="assessmentRequired"><see langword="null"/> leaves it unchanged.</param>
    /// <param name="assessmentResultRemarks">
    /// <see langword="null"/> leaves it unchanged; an empty string clears it. NOT required to save even
    /// when <paramref name="assessmentRequired"/> is true — see <see cref="EnsureAssessmentResultRecordedIfRequired"/>.
    /// </param>
    /// <param name="assignedClassTeacher">
    /// A Guid as text — <see langword="null"/> leaves it unchanged, an empty string clears it, the same
    /// three-state convention <c>UpdateArmCommand.FormTeacherAdminId</c> established for a
    /// nullable-reference field represented as a string.
    /// </param>
    /// <param name="declarationName"><see langword="null"/> leaves it unchanged; an empty string clears it.</param>
    /// <param name="declarationSigned">
    /// Setting this explicitly to <see langword="false"/> also clears <paramref name="declarationDate"/>
    /// on the record (an unsigned declaration cannot carry a signing date) regardless of what
    /// <paramref name="declarationDate"/> itself carries in the same request.
    /// </param>
    /// <param name="declarationDate">Required for the record to end up with <paramref name="declarationSigned"/> true; rejected while it is false.</param>
    /// <param name="headOfSchoolConfirmed"><see langword="null"/> leaves it unchanged.</param>
    /// <param name="headOfSchoolName"><see langword="null"/> leaves it unchanged; an empty string clears it.</param>
    /// <param name="asOfDate">"Today" — for the not-in-the-future checks.</param>
    public Result Update(
        Guid? sessionId,
        DateOnly? dateApplicationReceived,
        DateOnly? dateAdmitted,
        Guid? classAdmittedInto,
        AdmissionType? admissionType,
        string? admissionTypeNote,
        bool? assessmentRequired,
        string? assessmentResultRemarks,
        string? assignedClassTeacher,
        string? declarationName,
        bool? declarationSigned,
        DateOnly? declarationDate,
        bool? headOfSchoolConfirmed,
        string? headOfSchoolName,
        DateOnly asOfDate)
    {
        var nextDateApplicationReceived = dateApplicationReceived ?? DateApplicationReceived;

        if (nextDateApplicationReceived is { } receivedDate && receivedDate > asOfDate)
        {
            return Result.Failure(DateInFutureError(
                "admission_record.date_application_received_in_future", "Date application received", receivedDate));
        }

        var nextDateAdmitted = dateAdmitted ?? DateAdmitted;

        if (nextDateAdmitted > asOfDate)
        {
            return Result.Failure(DateInFutureError("admission_record.date_admitted_in_future", "Date admitted", nextDateAdmitted));
        }

        if (!TryNormalizeOptional(admissionTypeNote, AdmissionTypeNote, AdmissionTypeNoteMaxLength, "AdmissionTypeNote", out var nextNote, out var noteError))
        {
            return Result.Failure(noteError);
        }

        if (!TryNormalizeOptional(assessmentResultRemarks, AssessmentResultRemarks, AssessmentResultRemarksMaxLength, "AssessmentResultRemarks", out var nextRemarks, out var remarksError))
        {
            return Result.Failure(remarksError);
        }

        if (!TryParseOptionalGuid(assignedClassTeacher, AssignedClassTeacher, "AssignedClassTeacher", out var nextAssignedClassTeacher, out var teacherError))
        {
            return Result.Failure(teacherError);
        }

        if (!TryNormalizeOptional(declarationName, DeclarationName, DeclarationNameMaxLength, "DeclarationName", out var nextDeclarationName, out var declarationNameError))
        {
            return Result.Failure(declarationNameError);
        }

        if (!TryNormalizeOptional(headOfSchoolName, HeadOfSchoolName, HeadOfSchoolNameMaxLength, "HeadOfSchoolName", out var nextHeadOfSchoolName, out var headNameError))
        {
            return Result.Failure(headNameError);
        }

        // The declaration's signed flag and its date must agree once both this request's and the
        // record's existing values are reconciled — explicitly un-signing clears the date outright
        // (spec 6.5.9: the tick records that the signed paper exists, so an unsigned record cannot
        // also carry the date it was signed).
        var nextDeclarationSigned = declarationSigned ?? DeclarationSigned;
        DateOnly? nextDeclarationDate;

        if (declarationSigned is false)
        {
            nextDeclarationDate = null;
        }
        else
        {
            nextDeclarationDate = declarationDate ?? DeclarationDate;
        }

        if (nextDeclarationDate is { } signedDate && signedDate > asOfDate)
        {
            return Result.Failure(DateInFutureError("admission_record.declaration_date_in_future", "Declaration date", signedDate));
        }

        if (nextDeclarationSigned && nextDeclarationDate is null)
        {
            return Result.Failure(Error.Validation(
                "admission_record.declaration_date_required",
                "Enter the date the declaration was signed."));
        }

        if (!nextDeclarationSigned && nextDeclarationDate is not null)
        {
            return Result.Failure(Error.Validation(
                "admission_record.declaration_date_not_allowed",
                "A declaration date can only be recorded once the declaration is marked signed."));
        }

        SessionId = sessionId ?? SessionId;
        DateApplicationReceived = nextDateApplicationReceived;
        DateAdmitted = nextDateAdmitted;
        ClassAdmittedInto = classAdmittedInto ?? ClassAdmittedInto;
        AdmissionType = admissionType ?? AdmissionType;
        AdmissionTypeNote = nextNote;
        AssessmentRequired = assessmentRequired ?? AssessmentRequired;
        AssessmentResultRemarks = nextRemarks;
        AssignedClassTeacher = nextAssignedClassTeacher;
        DeclarationName = nextDeclarationName;
        DeclarationSigned = nextDeclarationSigned;
        DeclarationDate = nextDeclarationDate;
        HeadOfSchoolConfirmed = headOfSchoolConfirmed ?? HeadOfSchoolConfirmed;
        HeadOfSchoolName = nextHeadOfSchoolName;

        return Result.Success();
    }

    /// <summary>
    /// Spec 6.5.9: "Required where assessment_required is true and the admission is being approved."
    /// NOT enforced by <see cref="Update"/> — a required assessment with no recorded outcome saves
    /// fine, and only blocks the approval TASK-0051 has not built yet. No endpoint in this card calls
    /// this method; it exists so that card's approval handler has it ready to read.
    /// </summary>
    public Result EnsureAssessmentResultRecordedIfRequired()
    {
        if (AssessmentRequired && string.IsNullOrWhiteSpace(AssessmentResultRemarks))
        {
            return Result.Failure(Error.Validation(
                "admission_record.assessment_result_remarks_required_for_approval",
                "An assessment was required for this admission. Record the assessment result before approving."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Writes section J's approval fields (spec 6.5.9): who approved this record, and when. Called
    /// exactly once, by admission approval (TASK-0051), immediately after that same call has verified
    /// <see cref="EnsureAssessmentResultRecordedIfRequired"/> and <see cref="DeclarationSigned"/> —
    /// this method itself enforces neither, trusting the caller the way <see cref="Update"/> trusts
    /// its own inputs.
    /// </summary>
    public void RecordApproval(Guid? approvedBy, DateTimeOffset approvedAt)
    {
        ApprovedBy = approvedBy;
        ApprovedAt = approvedAt;
    }

    /// <summary>Records spec 6.5.16's reason for approving with the health answers missing. The caller has checked the privilege.</summary>
    public void RecordHealthOverride(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        HealthOverrideReason = reason.Trim();
    }

    private static Error DateInFutureError(string code, string fieldLabel, DateOnly value) =>
        Error.Validation(code, $"{fieldLabel} of {value:dd/MM/yyyy} cannot be in the future.");

    /// <summary>
    /// Resolves an optional string field's next value for <see cref="Update"/>: <paramref name="value"/>
    /// <see langword="null"/> (the field was absent from the partial payload) leaves
    /// <paramref name="currentValue"/> untouched; an empty string clears it; anything else is
    /// trimmed and length-checked.
    /// </summary>
    private static bool TryNormalizeOptional(
        string? value, string? currentValue, int maxLength, string fieldName, out string? normalized, out Error error)
    {
        if (value is null)
        {
            normalized = currentValue;
            error = Error.None;
            return true;
        }

        if (value.Length == 0)
        {
            normalized = null;
            error = Error.None;
            return true;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            normalized = null;
            error = Error.Validation(
                $"admission_record.{fieldName.ToLowerInvariant()}_too_long",
                $"{fieldName} must be at most {maxLength} characters.");
            return false;
        }

        normalized = trimmed;
        error = Error.None;
        return true;
    }

    /// <summary>Same absent/clear/set convention as <see cref="TryNormalizeOptional"/>, for a Guid represented as a string on the wire.</summary>
    private static bool TryParseOptionalGuid(string? value, Guid? currentValue, string fieldName, out Guid? parsed, out Error error)
    {
        if (value is null)
        {
            parsed = currentValue;
            error = Error.None;
            return true;
        }

        if (value.Length == 0)
        {
            parsed = null;
            error = Error.None;
            return true;
        }

        if (!Guid.TryParse(value, out var guid))
        {
            parsed = null;
            error = Error.Validation(
                $"admission_record.{fieldName.ToLowerInvariant()}_invalid",
                $"{fieldName} must be empty (to clear it) or a valid identifier.");
            return false;
        }

        parsed = guid;
        error = Error.None;
        return true;
    }
}
