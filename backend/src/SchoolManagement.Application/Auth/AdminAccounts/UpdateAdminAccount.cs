using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>
/// <c>PATCH /api/v1/admins/{id}</c> (spec 6.1.9, 6.1.14, 6.1.7 rule 4). Two authorisation shapes
/// reach this one command: an <c>admin.update</c> holder editing any account (name, email, phone,
/// and — rule 4 permitting — <see cref="IsSuperAdmin"/>), or the account itself editing only its OWN
/// <see cref="StaffName"/> and <see cref="Phone"/> (spec 6.1.2's self-edit carve-out — NOT email,
/// which the caller must hold <c>admin.update</c> to change even on their own account).
/// </summary>
/// <param name="Id">The account being edited.</param>
/// <param name="StaffName">Two words minimum, letters/spaces/hyphens/apostrophes only.</param>
/// <param name="Email">Login identifier. Self-edit callers must submit the account's current value unchanged.</param>
/// <param name="Phone">Nigerian format.</param>
/// <param name="IsSuperAdmin">
/// <see langword="null"/> to leave unchanged. A non-null value that differs from the account's
/// current flag is rule 4 territory (spec 6.1.7): only an acting admin who already holds
/// <see cref="AdminAccount.IsSuperAdmin"/> may change it, and a rejected attempt writes an audit
/// event even though the request as a whole still fails.
/// </param>
public sealed record UpdateAdminAccountCommand(
    Guid Id,
    string StaffName,
    string Email,
    string Phone,
    bool? IsSuperAdmin)
    : ICommand<Result<AdminAccountDetailDto>>;

/// <summary>Validates <see cref="UpdateAdminAccountCommand"/> against spec 6.1.3's field rules.</summary>
internal sealed class UpdateAdminAccountCommandValidator : AbstractValidator<UpdateAdminAccountCommand>
{
    public UpdateAdminAccountCommandValidator()
    {
        RuleFor(command => command.StaffName)
            .NotEmpty()
            .MaximumLength(AuthPolicy.StaffNameMaxLength);

        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(AuthPolicy.EmailMaxLength)
            .EmailAddress();

        RuleFor(command => command.Phone)
            .NotEmpty()
            .MaximumLength(AuthPolicy.PhoneMaxLength)
            .Must(phone => NigerianPhoneNumber.TryNormalize(phone, out _))
            .WithMessage(
                "Phone must be a valid Nigerian number: 11 digits starting with 0 " +
                "(for example 08012345678), or +234 followed by 10 digits.");
    }
}
