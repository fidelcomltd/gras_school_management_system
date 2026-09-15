using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// <c>POST /api/v1/pupils/{id}/registration-number</c> (spec 6.5.10, "Immutability and
/// correction"). <c>pupil.regnumber.correct</c>, Super Admin only — the privilege is excluded from
/// every seeded non-Super-Admin role (<c>SeededRoles.cs</c>). Unlike admission approval's issuance,
/// NOTHING here is generated: the administrator types the whole replacement number, because a
/// correction is usually fixing a wrong admission year and the corrected serial should be chosen
/// deliberately, never composed from settings.
/// </summary>
/// <param name="Id">The pupil whose number is being corrected. Supplied from the route, not the body.</param>
/// <param name="RegistrationNumber">The new number, typed in full. Must be well-formed and unique against both the live table and the history table.</param>
/// <param name="Reason">At least ten characters (spec 6.5.10). Written to the history row and to the audit event.</param>
public sealed record CorrectRegistrationNumberCommand(
    Guid Id,
    string RegistrationNumber,
    string Reason)
    : ICommand<Result<PupilDto>>;

/// <summary>Structural checks only — uniqueness against both tables and the "already has no number" state live in the handler.</summary>
internal sealed class CorrectRegistrationNumberCommandValidator : AbstractValidator<CorrectRegistrationNumberCommand>
{
    public CorrectRegistrationNumberCommandValidator()
    {
        RuleFor(command => command.RegistrationNumber)
            .NotEmpty()
            .MaximumLength(Pupil.RegistrationNumberMaxLength);

        // A SEPARATE chain: NotEmpty/MaximumLength above already reject blank or oversized input on
        // their own, so this only needs to add the shape check — combining it into one chain would
        // let .When suppress NotEmpty itself, since FluentValidation applies .When to every validator
        // already added to the same RuleFor chain, not just the one it is written after.
        RuleFor(command => command.RegistrationNumber)
            .Must(RegNumberFormat.IsWellFormed)
            .WithMessage("Registration number is not a valid shape (expected something like GRAS/2026/0041).")
            .When(command => !string.IsNullOrEmpty(command.RegistrationNumber) &&
                              command.RegistrationNumber.Length <= Pupil.RegistrationNumberMaxLength);

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MinimumLength(PupilRegNumberHistory.ReasonMinLength)
            .MaximumLength(PupilRegNumberHistory.ReasonMaxLength);
    }
}
