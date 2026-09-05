using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.ChangePassword;

/// <summary>
/// Self-service password change (spec 6.1.11, spec 6.1.14). Approved contract delta:
/// <c>POST /api/v1/auth/password</c>. The account comes from the caller's own session — this is never
/// how another account's password is changed (that is <c>admin.password.reset</c>, TASK-0019).
/// </summary>
/// <param name="CurrentPassword">Must match the account's current password (re-authentication for a
/// sensitive action).</param>
/// <param name="NewPassword">Must satisfy spec 6.1.11's composition rule and must not match any of the
/// last <see cref="AuthPolicy.PasswordHistoryLimit"/> hashes (checked in the handler, which needs the
/// password hasher — not expressible as a synchronous FluentValidation rule).</param>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword)
    : ICommand<Result<ChangePasswordResult>>;

/// <summary>
/// <see cref="ChangePasswordCommandHandler"/>'s result. <see cref="RawSessionToken"/> exists ONLY so
/// the endpoint can set the rotated session cookie — never part of <see cref="AuthSessionResponse"/>
/// and never serialised into the HTTP response body.
/// </summary>
/// <param name="Session">The wire-shaped session state, <c>mustChangePassword: false</c>.</param>
/// <param name="RawSessionToken">The new raw, unhashed session token (spec 9.1 rotates it on password change).</param>
public sealed record ChangePasswordResult(AuthSessionResponse Session, string RawSessionToken);

/// <summary>Validates <see cref="ChangePasswordCommand"/>'s composition rules (spec 6.1.11).</summary>
internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(AuthPolicy.PasswordMinLength)
            .WithMessage($"Password must be at least {AuthPolicy.PasswordMinLength} characters.")
            .MaximumLength(AuthPolicy.PasswordMaxLength)
            .WithMessage($"Password must be at most {AuthPolicy.PasswordMaxLength} characters.")
            .Must(password => password.Any(char.IsLetter))
            .WithMessage("Password must contain at least one letter.")
            .Must(password => password.Any(char.IsDigit))
            .WithMessage("Password must contain at least one digit.");
    }
}
