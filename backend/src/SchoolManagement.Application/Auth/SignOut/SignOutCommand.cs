using FluentValidation;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.SignOut;

/// <summary>
/// Signs the caller out (spec 6.1.14). Approved contract delta: <c>POST /api/v1/auth/sign-out</c>.
/// No request body — the session to revoke comes from <see cref="ICurrentSession"/>.
/// </summary>
public sealed record SignOutCommand : ICommand;

/// <summary>
/// Validates <see cref="SignOutCommand"/>. Empty: the command carries no fields, but
/// <c>ValidatorCoverageTests</c> still requires a validator to exist, recording that the question was
/// considered.
/// </summary>
internal sealed class SignOutCommandValidator : AbstractValidator<SignOutCommand>;

/// <summary>Handles <see cref="SignOutCommand"/>.</summary>
/// <remarks>
/// Always succeeds — approved contract delta §2: "Revokes the session if one exists; 204 either way,
/// including when called with no session or an already-dead one." A repeat sign-out is naturally
/// idempotent (TASK-0003's own scope-boundary framing), which is why this card needs no
/// <c>Idempotency-Key</c> mechanism for this endpoint.
/// </remarks>
internal sealed class SignOutCommandHandler(
    IAdminSessionRepository sessions,
    ICurrentSession currentSession,
    TimeProvider timeProvider)
    : IRequestHandler<SignOutCommand, Result>
{
    /// <inheritdoc />
    public async Task<Result> HandleAsync(SignOutCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentSession.SessionId is not { } sessionId)
        {
            return Result.Success();
        }

        var session = await sessions.FindTrackedByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        session?.Revoke(timeProvider.GetUtcNow(), AdminSessionRevocationReasons.SignOut);

        return Result.Success();
    }
}
