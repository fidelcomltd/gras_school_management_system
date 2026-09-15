using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Admissions;

/// <summary>
/// <c>PATCH /api/v1/admissions/{id}</c> (spec 6.5.9, 6.5.11 steps 1 and 8-9's section A/I fields, and
/// section J). <paramref name="Id"/> is the PUPIL id — the same identity <c>POST /pupils</c> (spec
/// 6.5.17's step 1, built as <c>POST /pupils</c> under TASK-0050) returned, and the one the admissions
/// queue already lists rows by.
/// </summary>
/// <remarks>
/// Every field below is independently optional — <see langword="null"/> leaves it unchanged (a
/// half-finished step still saves, spec 6.5.11), the same convention <c>UpdateArmCommand</c> and
/// <c>UpdatePupilBiographicalCommand</c> established; an empty string clears an optional string
/// field. <see cref="AdmissionRecord.ApprovedBy"/>/<see cref="AdmissionRecord.ApprovedAt"/> are
/// deliberately absent from this command — section J's approval fields are written only by admission
/// approval (TASK-0051), never through this endpoint.
/// </remarks>
/// <param name="Id">The pupil whose admission record is being edited. Supplied from the route, not the body.</param>
/// <param name="SessionId">Opaque id. Must reference an existing session.</param>
/// <param name="DateApplicationReceived">Not in the future.</param>
/// <param name="DateAdmitted">Not in the future.</param>
/// <param name="ClassAdmittedInto">Opaque id. Must reference an existing, active class level.</param>
/// <param name="AdmissionType">New or returning.</param>
/// <param name="AdmissionTypeNote">An empty string clears it.</param>
/// <param name="AssessmentRequired">Whether an entrance assessment is required.</param>
/// <param name="AssessmentResultRemarks">An empty string clears it. NOT required to save, even when <paramref name="AssessmentRequired"/> is true (spec 6.5.9) — see <see cref="AdmissionRecord.EnsureAssessmentResultRecordedIfRequired"/>.</param>
/// <param name="AssignedClassTeacher">Opaque admin-account id as text; an empty string clears it.</param>
/// <param name="DeclarationName">An empty string clears it.</param>
/// <param name="DeclarationSigned">
/// Section I's tick. Setting this <see langword="false"/> also clears the record's declaration date.
/// </param>
/// <param name="DeclarationDate">Required for the record to end up with <see cref="DeclarationSigned"/> true; rejected while it is false.</param>
/// <param name="HeadOfSchoolConfirmed">Section J's second signature block.</param>
/// <param name="HeadOfSchoolName">An empty string clears it.</param>
public sealed record UpdateAdmissionRecordCommand(
    Guid Id,
    string? SessionId,
    DateOnly? DateApplicationReceived,
    DateOnly? DateAdmitted,
    string? ClassAdmittedInto,
    AdmissionType? AdmissionType,
    string? AdmissionTypeNote,
    bool? AssessmentRequired,
    string? AssessmentResultRemarks,
    string? AssignedClassTeacher,
    string? DeclarationName,
    bool? DeclarationSigned,
    DateOnly? DeclarationDate,
    bool? HeadOfSchoolConfirmed,
    string? HeadOfSchoolName)
    : ICommand<Result<AdmissionRecordDto>>;

/// <summary>Structural checks only — session/level existence and the declaration-date cross-rule live in the handler and the entity.</summary>
internal sealed class UpdateAdmissionRecordCommandValidator : AbstractValidator<UpdateAdmissionRecordCommand>
{
    public UpdateAdmissionRecordCommandValidator()
    {
        RuleFor(command => command.SessionId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("SessionId must be a valid identifier.")
            .When(command => command.SessionId is not null);

        RuleFor(command => command.ClassAdmittedInto)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("ClassAdmittedInto must be a valid identifier.")
            .When(command => command.ClassAdmittedInto is not null);

        RuleFor(command => command.AdmissionType)
            .IsInEnum()
            .When(command => command.AdmissionType is not null);

        RuleFor(command => command.AdmissionTypeNote)
            .MaximumLength(AdmissionRecord.AdmissionTypeNoteMaxLength)
            .When(command => command.AdmissionTypeNote is not null);

        RuleFor(command => command.AssessmentResultRemarks)
            .MaximumLength(AdmissionRecord.AssessmentResultRemarksMaxLength)
            .When(command => command.AssessmentResultRemarks is not null);

        RuleFor(command => command.AssignedClassTeacher)
            .Must(value => value!.Length == 0 || Guid.TryParse(value, out _))
            .WithMessage("AssignedClassTeacher must be empty (to clear it) or a valid identifier.")
            .When(command => command.AssignedClassTeacher is not null);

        RuleFor(command => command.DeclarationName)
            .MaximumLength(AdmissionRecord.DeclarationNameMaxLength)
            .When(command => command.DeclarationName is not null);

        RuleFor(command => command.HeadOfSchoolName)
            .MaximumLength(AdmissionRecord.HeadOfSchoolNameMaxLength)
            .When(command => command.HeadOfSchoolName is not null);
    }
}
