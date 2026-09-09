using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// <c>PATCH /api/v1/pupils/{id}</c> (spec 6.5.4, 6.5.10). Biographical fields only. Every field is
/// independently optional — <see langword="null"/> leaves it unchanged, the same convention
/// <c>UpdateArmCommand</c> established; an empty string clears an optional field
/// (<paramref name="MiddleName"/>, <paramref name="PreviousSchool"/>, <paramref name="PreviousClass"/>,
/// <paramref name="OtherInformation"/>).
/// </summary>
/// <param name="Id">The pupil being edited.</param>
/// <param name="Surname">1..60 characters, letters/spaces/hyphens/apostrophes.</param>
/// <param name="FirstName">Same rule as <paramref name="Surname"/>.</param>
/// <param name="MiddleName">An empty string clears it.</param>
/// <param name="Sex">Male or female.</param>
/// <param name="DateOfBirth">In the past; must give an age between 2 and 20.</param>
/// <param name="Nationality">An empty string resets it to the default (<c>Nigerian</c>).</param>
/// <param name="StateOfOrigin">Must resolve to one of the 36 states or the Federal Capital Territory.</param>
/// <param name="Lga">Must resolve to one of <paramref name="StateOfOrigin"/>'s LGAs (the record's current state when <paramref name="StateOfOrigin"/> is not also supplied).</param>
/// <param name="HomeAddress">The child's own address.</param>
/// <param name="PreviousSchool">An empty string clears it.</param>
/// <param name="PreviousClass">An empty string clears it.</param>
/// <param name="OtherInformation">An empty string clears it.</param>
/// <param name="RegistrationNumber">
/// DECLARED ONLY SO ITS PRESENCE CAN BE DETECTED AND REJECTED — spec 6.5.10: "the number is
/// immutable and no ordinary edit path exists." Any non-null value here, including one identical to
/// the pupil's current number, is a 409. Never written.
/// </param>
public sealed record UpdatePupilBiographicalCommand(
    Guid Id,
    string? Surname,
    string? FirstName,
    string? MiddleName,
    PupilSex? Sex,
    DateOnly? DateOfBirth,
    string? Nationality,
    string? StateOfOrigin,
    string? Lga,
    string? HomeAddress,
    string? PreviousSchool,
    string? PreviousClass,
    string? OtherInformation,
    string? RegistrationNumber)
    : ICommand<Result<PupilDto>>;

/// <summary>Structural checks only — the age range, state/lga closed lists and character rules live in <see cref="Pupil.UpdateBiographical"/>.</summary>
internal sealed class UpdatePupilBiographicalCommandValidator : AbstractValidator<UpdatePupilBiographicalCommand>
{
    public UpdatePupilBiographicalCommandValidator()
    {
        RuleFor(command => command.Surname)
            .NotEmpty()
            .MaximumLength(Pupil.NameMaxLength)
            .When(command => command.Surname is not null);

        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(Pupil.NameMaxLength)
            .When(command => command.FirstName is not null);

        RuleFor(command => command.MiddleName)
            .MaximumLength(Pupil.NameMaxLength)
            .When(command => command.MiddleName is not null);

        RuleFor(command => command.Sex)
            .IsInEnum()
            .When(command => command.Sex is not null);

        RuleFor(command => command.StateOfOrigin)
            .NotEmpty()
            .When(command => command.StateOfOrigin is not null);

        RuleFor(command => command.Lga)
            .NotEmpty()
            .When(command => command.Lga is not null);

        RuleFor(command => command.HomeAddress)
            .NotEmpty()
            .MaximumLength(Pupil.HomeAddressMaxLength)
            .When(command => command.HomeAddress is not null);

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
