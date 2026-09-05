using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.Bootstrap;

/// <summary>
/// Creates the FIRST admin account (spec 6.1.6). OFF THE WIRE by design — see
/// <c>Api/Bootstrap/BootstrapAdminAccountCli.cs</c>, the only caller. No <c>MapPost</c> route ever
/// dispatches this command, so it never reaches <see cref="Domain.Security.PrivilegeDefinition"/>-style
/// authorisation or the deferred idempotency trigger (TASK-0013/TASK-0019): a command that cannot be
/// invoked twice through the same channel by an impatient client is not retry-duplicable in the sense
/// that trigger cares about, and the seed command's own refuse-if-any-account-exists rule below is the
/// mechanism spec 6.1.6 asks for instead.
/// </summary>
/// <param name="Email">The bootstrap account's login identifier. Format-validated, stored lower-invariant.</param>
/// <param name="StaffName">Display name. Two words minimum (spec 6.1.3).</param>
public sealed record BootstrapAdminAccountCommand(string Email, string StaffName)
    : ICommand<Result<BootstrapAdminAccountResponse>>;

/// <summary>
/// The bootstrap account's identity and its one-time plaintext password. Never served over HTTP —
/// the CLI prints <see cref="TemporaryPassword"/> to the console once and discards it.
/// </summary>
/// <param name="AccountId">The new account's opaque identifier.</param>
/// <param name="Email">The account's login identifier, as stored (lower-invariant).</param>
/// <param name="TemporaryPassword">
/// The randomly generated plaintext password. Exists only in this in-process return value; never
/// logged, never persisted (only its Argon2id hash is).
/// </param>
public sealed record BootstrapAdminAccountResponse(string AccountId, string Email, string TemporaryPassword);

/// <summary>Validates <see cref="BootstrapAdminAccountCommand"/>.</summary>
internal sealed class BootstrapAdminAccountCommandValidator : AbstractValidator<BootstrapAdminAccountCommand>
{
    public BootstrapAdminAccountCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(AuthPolicy.EmailMaxLength)
            .WithMessage($"Email must be at most {AuthPolicy.EmailMaxLength} characters.");

        RuleFor(command => command.StaffName)
            .NotEmpty().WithMessage("Staff name is required.")
            .Must(HaveAtLeastTwoWords).WithMessage("Staff name must contain at least two words.")
            .Matches(@"^[\p{L}\s'-]+$")
            .WithMessage("Staff name may contain only letters, spaces, hyphens and apostrophes.")
            .MaximumLength(AuthPolicy.StaffNameMaxLength)
            .WithMessage($"Staff name must be at most {AuthPolicy.StaffNameMaxLength} characters.");
    }

    private static bool HaveAtLeastTwoWords(string staffName) =>
        staffName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length >= 2;
}
