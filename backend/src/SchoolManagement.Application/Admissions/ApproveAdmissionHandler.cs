using System.Globalization;
using SchoolManagement.Application.Abstractions.Admissions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Admissions;

/// <summary>Handles <see cref="ApproveAdmissionCommand"/>.</summary>
/// <remarks>
/// <para>
/// School-wide <c>pupil.admission.approve</c> only (see <c>AdmissionEndpoints.MapEndpoints</c>) — a
/// pending pupil has no open enrolment, so an arm-scoped grant could never resolve a target arm here,
/// the same reasoning <c>UpdateAdmissionRecordHandler</c> and <c>ListAdmissionsQueueQuery</c> already
/// established for this record shape.
/// </para>
/// <para>
/// THE NUMBER ITSELF is issued by <see cref="RegistrationNumberIssuer"/> — the counter row lock, the
/// composition and the retry-on-duplicate loop (spec 6.5.10), shared with bulk import.
/// </para>
/// </remarks>
internal sealed class ApproveAdmissionCommandHandler(
    IPupilRepository pupils,
    IAdmissionRecordRepository admissionRecords,
    IArmRepository arms,
    IClassLevelRepository classLevels,
    IAcademicSessionRepository sessions,
    ITermRepository terms,
    IEnrolmentRepository enrolments,
    RegistrationNumberIssuer issuer,
    ISchoolProfileRepository schoolProfiles,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider,
    Pupils.Records.AdmissionCompleteness completeness)
    : IRequestHandler<ApproveAdmissionCommand, Result<PupilDto>>
{
    private const string PupilEntityType = "pupil";
    private const string ArmEntityType = "arm";

    /// <inheritdoc />
    public async Task<Result<PupilDto>> HandleAsync(ApproveAdmissionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pupil = await pupils.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (pupil is null)
        {
            return Result.Failure<PupilDto>(Error.NotFound(
                "admission.not_found", "No pending admission was found with that id."));
        }

        // Checked BEFORE touching anything else: an already-resolved record (approved, declined, or
        // any other non-pending status) is a 409, not a 404 — acceptance criterion "409 on an
        // already-approved record".
        if (pupil.Status != PupilStatus.Pending)
        {
            return Result.Failure<PupilDto>(Error.Conflict(
                "admission.already_approved",
                "This admission is no longer pending — it has already been approved, declined, or otherwise resolved."));
        }

        var record = await admissionRecords.FindTrackedByPupilIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            // Cannot happen given CreatePupilHandler's same-transaction invariant — defensive, not a
            // reachable branch in normal operation (UpdateAdmissionRecordHandler's own pattern).
            return Result.Failure<PupilDto>(Error.NotFound(
                "admission.not_found", "No pending admission was found with that id."));
        }

        // Blocking condition: no active term (spec 6.5.11, verbatim message).
        if (await terms.FindActiveAsync(cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<PupilDto>(Error.Conflict(
                "admission.no_active_term",
                "No term is currently active. Open a term before approving admissions."));
        }

        var activeSession = await sessions.FindActiveAsync(cancellationToken).ConfigureAwait(false);
        var armId = Guid.Parse(request.ArmId);
        var arm = activeSession is null ? null : await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);

        // Blocking condition: no arm for the level in the active session (spec 6.5.11). A null active
        // session vacuously satisfies "no arm exists for the level in the active session" — there is
        // no active session for any arm to belong to.
        if (activeSession is null ||
            arm is null ||
            arm.ClassLevelId != record.ClassAdmittedInto ||
            arm.SessionId != activeSession.Id ||
            arm.Status == ArmStatus.Closed)
        {
            return Result.Failure<PupilDto>(Error.Validation(
                "admission.arm_not_available",
                "No arm is available for this pupil's class level in the active session. Choose a different arm, or create one for this level first."));
        }

        var allLevels = await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var level = allLevels.FirstOrDefault(candidate => candidate.Id == arm.ClassLevelId);
        var armDisplayName = level is null ? arm.Label : ArmDisplayName.Compose(level.Name, arm.Label);

        // Capacity (spec 6.4.6): a SOFT limit. A WARNING with an audited override, never an outright
        // rejection — over capacity blocks only a caller who does NOT hold arm.capacity.override.
        var openCount = await enrolments.CountOpenExcludingPendingByArmAsync(arm.Id, cancellationToken).ConfigureAwait(false);

        if (openCount >= arm.Capacity)
        {
            var grants = await effectivePrivilegeProvider
                .GetGrantsAsync(currentUser.UserId ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);

            var overrideScope = PupilAccessGuard.Resolve(grants, Privileges.Arm.CapacityOverride);
            var hasOverride = overrideScope switch
            {
                PupilAccessScope.SchoolWide => true,
                PupilAccessScope.ArmRestricted => PupilAccessGuard.ResolveArmIds(grants, Privileges.Arm.CapacityOverride).Contains(arm.Id),
                _ => false,
            };

            if (!hasOverride)
            {
                return Result.Failure<PupilDto>(Error.Conflict(
                    "arm.at_capacity",
                    $"{armDisplayName} is at its capacity of {arm.Capacity}. Raise the capacity or choose another arm."));
            }

            // Spec 6.4.6: "the override is written to the audit log with the arm, the pupil and the
            // resulting count" — a distinct audit event from the approval itself.
            await auditSink.RecordAsync(
                Privileges.Arm.CapacityOverride,
                ArmEntityType,
                arm.Id.ToString("D", CultureInfo.InvariantCulture),
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["pupilId"] = pupil.Id.ToString("D", CultureInfo.InvariantCulture),
                    ["capacity"] = arm.Capacity,
                    ["resultingCount"] = openCount + 1,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);
        }

        var profile = await schoolProfiles.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var resolvedHeadOfSchoolName = request.HeadOfSchoolName ?? profile.HeadTeacherName;

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // Section J's assessment outcome and head-of-school fields — reuses Update's own
        // null-means-unchanged convention rather than a second setter (every other field left null).
        var recordUpdate = record.Update(
            sessionId: null,
            dateApplicationReceived: null,
            dateAdmitted: null,
            classAdmittedInto: null,
            admissionType: null,
            admissionTypeNote: null,
            assessmentRequired: null,
            assessmentResultRemarks: request.AssessmentResultRemarks,
            assignedClassTeacher: null,
            declarationName: null,
            declarationSigned: null,
            declarationDate: null,
            headOfSchoolConfirmed: request.HeadOfSchoolConfirmed,
            headOfSchoolName: resolvedHeadOfSchoolName,
            asOfDate: today);

        if (recordUpdate.IsFailure)
        {
            return Result.Failure<PupilDto>(recordUpdate.Error);
        }

        // Blocking condition: a required assessment with no recorded outcome (spec 6.5.9/6.5.11).
        var assessmentCheck = record.EnsureAssessmentResultRecordedIfRequired();

        if (assessmentCheck.IsFailure)
        {
            return Result.Failure<PupilDto>(assessmentCheck.Error);
        }

        // Blocking condition: declaration not signed (spec 6.5.11 step 8/9).
        if (!record.DeclarationSigned)
        {
            return Result.Failure<PupilDto>(Error.Validation(
                "admission.declaration_not_signed",
                "Declaration (Section I) has not been signed. Sign the declaration before approving."));
        }

        // Blocking conditions from steps 3 to 5 (spec 6.5.12): contacts, the barred-persons answer, the health answers.
        // The declaration and assessment were checked just above against this command's own unsaved changes.
        var missing = await completeness.BlockingSectionsAsync(pupil.Id, cancellationToken).ConfigureAwait(false);

        // Spec 6.5.16: a parent who declines the health questions leaves the record pending; the head teacher
        // "can approve with a reason". Only the health gap is waivable, and only by pupil.admission.override.
        var healthOverridden = request.HealthOverrideReason is not null
            && missing.Count > 0
            && missing.All(item => item.Code == Pupils.Records.AdmissionCompleteness.HealthUnansweredCode);
        if (missing.Count > 0 && !healthOverridden)
        {
            return Result.Failure<PupilDto>(Error.Validation(
                "admission.incomplete",
                "This admission is not complete. " + string.Join(" ", missing.Select(item => $"Step {item.Step}: {item.Message}"))));
        }

        if (healthOverridden)
        {
            var grants = await effectivePrivilegeProvider
                .GetGrantsAsync(currentUser.UserId ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
            if (PupilAccessGuard.Resolve(grants, Privileges.Pupil.AdmissionOverride) != PupilAccessScope.SchoolWide)
            {
                return Result.Failure<PupilDto>(Error.Forbidden(
                    "admission.override_forbidden",
                    "Approving without the health answers needs the admission override privilege. Ask the head teacher."));
            }
        }

        var actorId = currentUser.UserId is { } actorIdText && Guid.TryParse(actorIdText, out var parsedActorId)
            ? parsedActorId
            : (Guid?)null;
        var now = timeProvider.GetUtcNow();

        record.RecordApproval(actorId, now);
        if (healthOverridden)
        {
            record.RecordHealthOverride(request.HealthOverrideReason!);
        }

        var approveResult = pupil.Approve();

        if (approveResult.IsFailure)
        {
            return Result.Failure<PupilDto>(approveResult.Error);
        }

        // Enrolment effective date (spec 6.5.11): "the admission date or the session start date,
        // whichever is later."
        var effectiveFrom = record.DateAdmitted > activeSession.StartDate ? record.DateAdmitted : activeSession.StartDate;

        var enrolmentCreation = Enrolment.Open(Guid.CreateVersion7(), pupil.Id, arm.Id, effectiveFrom);

        if (enrolmentCreation.IsFailure)
        {
            return Result.Failure<PupilDto>(enrolmentCreation.Error);
        }

        // Added ONCE, outside the retry loop: a retry re-attempts SaveChangesAsync against the SAME
        // tracked "Added" enrolment, never a second Add — EF Core's automatic savepoint rollback
        // (see the type remarks) restores a failed attempt's tracked entities to be re-saved, not
        // re-created.
        await enrolments.AddAsync(enrolmentCreation.Value, cancellationToken).ConfigureAwait(false);

        // THE YEAR IS admission_record.date_admitted's YEAR, NEVER TODAY'S (spec 6.5.10, acceptance
        // criterion 3). Abbreviation/separator/serialWidth/serialReset are read from SAVED settings
        // and frozen into the composed string — never the preview endpoint's unsaved query
        // parameters (acceptance criterion 4).
        var issued = await issuer.IssueAndSaveAsync(pupil, record.DateAdmitted.Year, profile, cancellationToken).ConfigureAwait(false);

        if (issued.IsFailure)
        {
            return Result.Failure<PupilDto>(issued.Error);
        }

        var issuedRegistrationNumber = issued.Value;

        await auditSink.RecordAsync(
            Privileges.Pupil.AdmissionApprove,
            PupilEntityType,
            pupil.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["outcome"] = "approved",
                ["registrationNumber"] = issuedRegistrationNumber,
                ["armId"] = arm.Id.ToString("D", CultureInfo.InvariantCulture),
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        if (healthOverridden)
        {
            // A distinct event, as the capacity override is: who waived the health answers. The reason itself stays on the
            // admission record, because it may describe the child's health (audit readers need not hold safeguarding view).
            await auditSink.RecordAsync(
                Privileges.Pupil.AdmissionOverride,
                PupilEntityType,
                pupil.Id.ToString("D", CultureInfo.InvariantCulture),
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["waived"] = Pupils.Records.AdmissionCompleteness.HealthUnansweredCode,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);
        }

        var admissionDto = AdmissionRecordMapper.ToDto(record, level?.Name);

        return Result.Success(PupilMapper.ToDto(pupil, today, admission: admissionDto));
    }
}
