using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Admissions;

/// <summary>
/// <c>POST /api/v1/admissions/{id}/approve</c> (spec 6.5.10, 6.5.11 step 9, 6.5.14). <paramref
/// name="Id"/> is the PUPIL id, the same identity <c>PATCH /admissions/{id}</c> already uses. The
/// only route into <c>PupilStatus.Active</c> for a new record: in ONE transaction, issues the
/// registration number (spec 6.5.10), writes section J, and opens the first enrolment in <see
/// cref="ArmId"/>. <c>Idempotency-Key</c> is REQUIRED on this route (spec 6.5.17) — see
/// <c>AdmissionEndpoints.MapApprove</c>.
/// </summary>
/// <param name="Id">The pupil whose pending admission is being approved. Supplied from the route, not the body.</param>
/// <param name="ArmId">
/// Opaque id. Must reference an arm for the record's <c>class_admitted_into</c> level, in the
/// currently active session (spec 6.5.11 step 9: "The selector lists the level's arms for the active
/// session"). Over capacity is a WARNING (spec 6.4.6), not a rejection, for a caller holding
/// <c>arm.capacity.override</c>.
/// </param>
/// <param name="AssessmentResultRemarks">
/// The assessment outcome (spec 6.5.11 step 9). Required — and checked against the record's OWN
/// <c>assessment_required</c> flag, not this command's shape — where an assessment was required;
/// <see langword="null"/> otherwise leaves whatever the record already stored unchanged.
/// </param>
/// <param name="HeadOfSchoolConfirmed">
/// Section J's second signature block (appendix B question 26: "one approving account plus a
/// confirmation tick for the head of school, both required at approval"). Must be <see
/// langword="true"/> — <see langword="false"/> blocks approval outright.
/// </param>
/// <param name="HeadOfSchoolName">
/// Section J. Omitted defaults to <c>settings.head_teacher_name</c> (spec 6.5.9: "Defaults from
/// settings" — the orchestrator's ruling on the source field, drift 2026-09-15). A supplied value
/// always wins over the default.
/// </param>
/// <param name="HealthOverrideReason">
/// Spec 6.5.16: the parent declined to answer the health questions, and a holder of
/// <c>pupil.admission.override</c> approves anyway, saying why (10 to 500 characters). Waives ONLY the
/// unanswered health questions; every other blocking item still blocks. The approval is audited; the reason is kept on
/// the admission record, not in the audit log. Omit otherwise.
/// </param>
public sealed record ApproveAdmissionCommand(
    Guid Id,
    string ArmId,
    string? AssessmentResultRemarks,
    bool HeadOfSchoolConfirmed,
    string? HeadOfSchoolName,
    string? HealthOverrideReason = null)
    : ICommand<Result<PupilDto>>;

/// <summary>Structural checks only — arm/level/session/term state and the assessment/declaration blocking conditions live in the handler.</summary>
internal sealed class ApproveAdmissionCommandValidator : AbstractValidator<ApproveAdmissionCommand>
{
    public ApproveAdmissionCommandValidator()
    {
        RuleFor(command => command.ArmId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");

        RuleFor(command => command.AssessmentResultRemarks)
            .MaximumLength(AdmissionRecord.AssessmentResultRemarksMaxLength)
            .When(command => command.AssessmentResultRemarks is not null);

        // Appendix B question 26 / this card's acceptance criteria: false blocks approval outright,
        // with its own test — a structural, input-shaped rule, unlike the state-dependent blocking
        // conditions (arm/term/assessment/declaration) the handler checks against loaded data.
        RuleFor(command => command.HeadOfSchoolConfirmed)
            .Equal(true)
            .WithMessage("Section J's head-of-school confirmation must be ticked before this admission can be approved.");

        RuleFor(command => command.HeadOfSchoolName)
            .MaximumLength(AdmissionRecord.HeadOfSchoolNameMaxLength)
            .When(command => command.HeadOfSchoolName is not null);

        RuleFor(command => command.HealthOverrideReason)
            .Must(reason => reason!.Trim().Length is >= 10 and <= AdmissionRecord.HealthOverrideReasonMaxLength)
            .WithMessage("Give a reason of 10 to 500 characters for approving without the health answers.")
            .When(command => command.HealthOverrideReason is not null);
    }
}
