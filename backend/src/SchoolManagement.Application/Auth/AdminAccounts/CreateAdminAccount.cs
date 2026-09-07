using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Idempotency;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>
/// <c>POST /api/v1/admins</c> (spec 6.1.9 step 1, 6.1.14): "Step one takes staff name, email and
/// phone and creates the account." Role assignment (step two) is TASK-0028 — an account with zero
/// assignments can exist and sign in, per spec 6.1.9's own text.
/// </summary>
/// <param name="StaffName">Two words minimum, letters/spaces/hyphens/apostrophes only (spec 6.1.3).</param>
/// <param name="Email">Login identifier. Unique across active and suspended accounts (spec 6.1.3).</param>
/// <param name="Phone">Nigerian format — <c>08012345678</c> or <c>+2348012345678</c>.</param>
public sealed record CreateAdminAccountCommand(string StaffName, string Email, string Phone)
    : ICommand<Result<CreateAdminAccountResponse>>;

/// <summary>
/// The created account, including the one-time temporary password (spec 6.1.9: "displays it once on
/// screen with a copy button, and never displays it again").
/// </summary>
/// <param name="Id">Opaque identifier.</param>
/// <param name="StaffName">Display name.</param>
/// <param name="Email">Login identifier.</param>
/// <param name="Phone">Normalised <c>+234</c> form.</param>
/// <param name="Status">Always <c>Active</c> on creation (spec 6.1.10).</param>
/// <param name="MustChangePassword">Always <see langword="true"/> on creation (spec 6.1.3).</param>
/// <param name="CreatedAtUtc">When the account was created.</param>
/// <param name="TemporaryPassword">
/// The generated plaintext password. Present on the live response; REDACTED (<see langword="null"/>)
/// on a stored idempotency replay — see <see cref="RedactFromIdempotencyReplayAttribute"/> and the
/// approved delta's orchestrator amendment A2 (spec 6.1.9/6.1.14: shown once, never again — a replay
/// that returned it verbatim would be a second display).
/// </param>
public sealed record CreateAdminAccountResponse(
    string Id,
    string StaffName,
    string Email,
    string Phone,
    AdminAccountStatus Status,
    bool MustChangePassword,
    DateTimeOffset CreatedAtUtc,
    [property: RedactFromIdempotencyReplay] string? TemporaryPassword);

/// <summary>Validates <see cref="CreateAdminAccountCommand"/> against spec 6.1.3's field rules.</summary>
internal sealed class CreateAdminAccountCommandValidator : AbstractValidator<CreateAdminAccountCommand>
{
    public CreateAdminAccountCommandValidator()
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
