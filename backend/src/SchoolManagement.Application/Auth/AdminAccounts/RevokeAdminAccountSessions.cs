using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>
/// <c>DELETE /api/v1/admins/{id}/sessions</c> (spec 6.1.14): "Revoke all sessions for the account."
/// Human §5 sign-off (2026-09-06): the <c>admin.session.revoke</c> grant is the whole gate.
/// </summary>
/// <param name="Id">The account whose sessions are being revoked.</param>
public sealed record RevokeAdminAccountSessionsCommand(Guid Id) : ICommand;

/// <summary>
/// Trivial but mandatory — the command carries no field beyond the route-bound <c>Id</c>.
/// </summary>
internal sealed class RevokeAdminAccountSessionsCommandValidator
    : AbstractValidator<RevokeAdminAccountSessionsCommand>;
