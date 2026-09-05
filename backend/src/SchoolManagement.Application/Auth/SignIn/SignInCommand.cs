using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.SignIn;

/// <summary>
/// Signs an administrator in (spec 6.1.11, spec 9.1). Approved contract delta:
/// <c>POST /api/v1/auth/sign-in</c>.
/// </summary>
/// <param name="Email">The account's login identifier. <see cref="SignInCommandValidator"/> DOES
/// check this is a well-formed email address — that check runs (and can reject) before the Argon2id
/// verify, but it does not reopen spec 6.1.11's timing concern: format validation happens identically
/// whether or not any account with that shape of address exists, so it cannot distinguish "known
/// email" from "unknown email" the way the Argon2id verify's presence/absence would.</param>
/// <param name="Password">The submitted plaintext password. Never logged (redacted by field name — see
/// <c>RedactSensitivePropertiesEnricher</c> — and never included in the request-logging behaviour's
/// output in the first place, since that behaviour logs only the request TYPE name).</param>
public sealed record SignInCommand(string Email, string Password) : ICommand<Result<SignInResult>>;

/// <summary>
/// <see cref="SignInCommandHandler"/>'s result. <see cref="RawSessionToken"/> exists ONLY so the
/// endpoint can set the session cookie — it is never part of <see cref="AuthSessionResponse"/> and
/// must never be serialised into the HTTP response body.
/// </summary>
/// <param name="Session">The wire-shaped session state.</param>
/// <param name="SessionId">The new session's id, needed by the endpoint to bind the CSRF cookie to it.</param>
/// <param name="RawSessionToken">The raw, unhashed session token. Set as the <c>__Host-Session</c> cookie value.</param>
public sealed record SignInResult(AuthSessionResponse Session, Guid SessionId, string RawSessionToken);

/// <summary>Validates <see cref="SignInCommand"/>.</summary>
internal sealed class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public SignInCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Password is required.")
            // Aligned with ChangePasswordCommandValidator's NewPassword cap (second-pass review LOW
            // 8). Safe against the timing-oracle concern above for the same reason format validation
            // is: this rejection happens before any account lookup, so it takes identical time
            // whether the email names a real account or not.
            .MaximumLength(AuthPolicy.PasswordMaxLength)
            .WithMessage($"Password must be at most {AuthPolicy.PasswordMaxLength} characters.");
    }
}
