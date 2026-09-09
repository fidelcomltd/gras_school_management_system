using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// <c>POST /api/v1/pupils</c> (spec 6.5.4). Always creates a <see cref="PupilStatus.Pending"/>
/// record with a <see langword="null"/> registration number — see the task card's own goal: "Nothing
/// can reach active yet."
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
    string? OtherInformation)
    : ICommand<Result<PupilDto>>;

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
    }
}
