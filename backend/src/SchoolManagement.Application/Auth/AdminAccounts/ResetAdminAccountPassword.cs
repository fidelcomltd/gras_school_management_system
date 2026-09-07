using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Idempotency;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>
/// <c>POST /api/v1/admins/{id}/password-reset</c> (spec 6.1.11, 6.1.14): "A forced reset by
/// <c>admin.password.reset</c> sets a new temporary password, displays it once, sets
/// <c>must_change_password</c>, and revokes every active session for the account." Human §5
/// sign-off (2026-09-06): the privilege grant is the whole gate — no step-up re-authentication of
/// the acting admin, no additional <c>is_super_admin</c> requirement.
/// </summary>
/// <param name="Id">The account whose password is being force-reset.</param>
public sealed record ResetAdminAccountPasswordCommand(Guid Id)
    : ICommand<Result<ResetAdminAccountPasswordResponse>>;

/// <summary>
/// The new one-time temporary password (spec 6.1.11: "displays it once").
/// </summary>
/// <param name="Id">The account.</param>
/// <param name="TemporaryPassword">
/// The generated plaintext password. Present on the live response; REDACTED (<see langword="null"/>)
/// on a stored idempotency replay — see <see cref="RedactFromIdempotencyReplayAttribute"/>.
/// </param>
public sealed record ResetAdminAccountPasswordResponse(
    string Id,
    [property: RedactFromIdempotencyReplay] string? TemporaryPassword);

/// <summary>
/// Trivial but mandatory — the command carries no field beyond the route-bound <c>Id</c>.
/// </summary>
internal sealed class ResetAdminAccountPasswordCommandValidator
    : AbstractValidator<ResetAdminAccountPasswordCommand>;
