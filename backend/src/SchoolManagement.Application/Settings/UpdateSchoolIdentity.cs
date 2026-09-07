using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>PATCH /api/v1/settings/identity</c> (spec 6.2.3). <see cref="SchoolProfile.Abbreviation"/> and
/// <see cref="SchoolProfile.Timezone"/> are deliberately absent from this body — the abbreviation is
/// TASK-0005c's own endpoint, and the timezone is fixed and not editable in this version.
/// </summary>
/// <param name="SchoolName">Full school name.</param>
/// <param name="ShortName">Used where the full name will not fit.</param>
/// <param name="Address">Multi-line permitted.</param>
/// <param name="Phone">Nigerian format — <c>08012345678</c> or <c>+2348012345678</c>.</param>
/// <param name="Email">Valid email format.</param>
/// <param name="Motto"><see langword="null"/> to leave the school with no motto.</param>
/// <param name="HeadTeacherName">Printed above the head teacher's signature block.</param>
/// <param name="ExpectedVersion">
/// The identity group's current <c>versionNumber</c>, as last read from <c>GET /settings</c>. A
/// stale value is rejected <c>409 settings.identity.stale_version</c> before anything is written.
/// </param>
public sealed record UpdateSchoolIdentityCommand(
    string SchoolName,
    string ShortName,
    string Address,
    string Phone,
    string Email,
    string? Motto,
    string HeadTeacherName,
    int ExpectedVersion)
    : ICommand<Result<SettingsIdentityGroupDto>>;

/// <summary>Validates <see cref="UpdateSchoolIdentityCommand"/> against spec 6.2.3's field rules.</summary>
internal sealed class UpdateSchoolIdentityCommandValidator : AbstractValidator<UpdateSchoolIdentityCommand>
{
    public UpdateSchoolIdentityCommandValidator()
    {
        RuleFor(command => command.SchoolName)
            .NotEmpty()
            .MaximumLength(SchoolProfile.SchoolNameMaxLength);

        RuleFor(command => command.ShortName)
            .NotEmpty()
            .MaximumLength(SchoolProfile.ShortNameMaxLength);

        RuleFor(command => command.Address)
            .NotEmpty()
            .MaximumLength(SchoolProfile.AddressMaxLength);

        RuleFor(command => command.Phone)
            .NotEmpty()
            .MaximumLength(SchoolProfile.PhoneMaxLength)
            .Must(phone => NigerianPhoneNumber.TryNormalize(phone, out _))
            .WithMessage(
                "Phone must be a valid Nigerian number: 11 digits starting with 0 " +
                "(for example 08012345678), or +234 followed by 10 digits.");

        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(SchoolProfile.EmailMaxLength)
            .EmailAddress();

        RuleFor(command => command.Motto)
            .MaximumLength(SchoolProfile.MottoMaxLength)
            .When(command => command.Motto is not null);

        RuleFor(command => command.HeadTeacherName)
            .NotEmpty()
            .MaximumLength(SchoolProfile.HeadTeacherNameMaxLength);

        RuleFor(command => command.ExpectedVersion)
            .GreaterThanOrEqualTo(0);
    }
}
