using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// <c>POST /api/v1/pupils</c> (spec 6.5.4, 6.5.9). Always creates a <see cref="PupilStatus.Pending"/>
/// record with a <see langword="null"/> registration number — see the task card's own goal: "Nothing
/// can reach active yet." <paramref name="Admission"/> is section A of the paper form, saved as the
/// pupil's <c>admission_record</c> row in the SAME transaction (TASK-0062) — a pupil with no
/// admission record is a state this handler cannot produce.
/// </summary>
/// <param name="Surname">1..60 characters, letters/spaces/hyphens/apostrophes.</param>
/// <param name="FirstName">Same rule as <paramref name="Surname"/>.</param>
/// <param name="MiddleName">Optional. Same rule as <paramref name="Surname"/> when supplied.</param>
/// <param name="Sex">Male or female.</param>
/// <param name="DateOfBirth">In the past; must give an age between 2 and 20.</param>
/// <param name="Nationality"><see langword="null"/> defaults to <c>Nigerian</c>.</param>
/// <param name="StateOfOrigin">One of the 36 states or the Federal Capital Territory — free text is rejected.</param>
/// <param name="Lga">Must belong to <paramref name="StateOfOrigin"/> — free text is rejected.</param>
/// <param name="HomeAddress">The child's own address. Multi-line permitted.</param>
/// <param name="PreviousSchool">Optional.</param>
/// <param name="PreviousClass">Optional.</param>
/// <param name="OtherInformation">Optional, section G free text.</param>
/// <param name="Admission">Section A. Required — every pupil gets an admission record at creation.</param>
public sealed record CreatePupilCommand(
    string Surname,
    string FirstName,
    string? MiddleName,
    PupilSex Sex,
    DateOnly DateOfBirth,
    string? Nationality,
    string StateOfOrigin,
    string Lga,
    string HomeAddress,
    string? PreviousSchool,
    string? PreviousClass,
    string? OtherInformation,
    CreateAdmissionInput Admission)
    : ICommand<Result<PupilDto>>;

/// <summary>
/// Section A of the admission form, as captured at pupil creation (spec 6.5.9). Sections I and J are
/// filled in later, through <c>PATCH /admissions/{id}</c> (TASK-0062).
/// </summary>
/// <param name="SessionId">Opaque id. <see langword="null"/> defaults to the active session.</param>
/// <param name="DateApplicationReceived">Optional; not in the future.</param>
/// <param name="DateAdmitted"><see langword="null"/> defaults to today; not in the future.</param>
/// <param name="ClassAdmittedInto">Opaque id. Must reference an existing, active class level.</param>
/// <param name="AdmissionType">New or returning.</param>
/// <param name="AdmissionTypeNote">Optional.</param>
/// <param name="AssessmentRequired">Explicit yes or no.</param>
public sealed record CreateAdmissionInput(
    string? SessionId,
    DateOnly? DateApplicationReceived,
    DateOnly? DateAdmitted,
    string ClassAdmittedInto,
    AdmissionType AdmissionType,
    string? AdmissionTypeNote,
    bool AssessmentRequired);

/// <summary>Structural checks only — the age range, state/lga closed lists and character rules live in <see cref="Pupil.Create"/>.</summary>
internal sealed class CreatePupilCommandValidator : AbstractValidator<CreatePupilCommand>
{
    public CreatePupilCommandValidator()
    {
        RuleFor(command => command.Surname).NotEmpty().MaximumLength(Pupil.NameMaxLength);
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(Pupil.NameMaxLength);

        RuleFor(command => command.MiddleName)
            .MaximumLength(Pupil.NameMaxLength)
            .When(command => command.MiddleName is not null);

        RuleFor(command => command.Sex).IsInEnum();

        RuleFor(command => command.StateOfOrigin).NotEmpty();
        RuleFor(command => command.Lga).NotEmpty();
        RuleFor(command => command.HomeAddress).NotEmpty().MaximumLength(Pupil.HomeAddressMaxLength);

        RuleFor(command => command.PreviousSchool)
            .MaximumLength(Pupil.PreviousSchoolMaxLength)
            .When(command => command.PreviousSchool is not null);

        RuleFor(command => command.PreviousClass)
            .MaximumLength(Pupil.PreviousClassMaxLength)
            .When(command => command.PreviousClass is not null);

        RuleFor(command => command.OtherInformation)
            .MaximumLength(Pupil.OtherInformationMaxLength)
            .When(command => command.OtherInformation is not null);

        RuleFor(command => command.Admission).NotNull();
        RuleFor(command => command.Admission)
            .SetValidator(new CreateAdmissionInputValidator())
            .When(command => command.Admission is not null);
    }
}

/// <summary>Structural checks only — session/level existence lives in <see cref="CreatePupilHandler"/>.</summary>
internal sealed class CreateAdmissionInputValidator : AbstractValidator<CreateAdmissionInput>
{
    public CreateAdmissionInputValidator()
    {
        RuleFor(input => input.SessionId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("SessionId must be a valid identifier.")
            .When(input => input.SessionId is not null);

        RuleFor(input => input.ClassAdmittedInto)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("ClassAdmittedInto must be a valid identifier.");

        RuleFor(input => input.AdmissionType).IsInEnum();

        RuleFor(input => input.AdmissionTypeNote)
            .MaximumLength(AdmissionRecord.AdmissionTypeNoteMaxLength)
            .When(input => input.AdmissionTypeNote is not null);
    }
}
