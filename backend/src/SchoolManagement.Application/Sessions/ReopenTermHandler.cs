using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Sessions;

/// <summary>
/// Handles <see cref="ReopenTermCommand"/>.
/// </summary>
/// <remarks>
/// ASSUMPTION (recorded in <c>backend/docs/ASSUMPTIONS.md</c>): spec 6.3.6 refuses reopening when
/// "the following term has already been opened," but this codebase has no direct FK from Third Term
/// to "the next session's First Term." For ordinal 3, this handler reads it off the session's own
/// <see cref="SessionState"/> instead: <see cref="AcademicSession.Close"/> is only ever called (by
/// <see cref="OpenTermHandler"/>) as a side effect of a successor session's First Term opening, so
/// <see cref="SessionState.Closed"/> on THIS term's session is exactly "a following term opened." This
/// also happens to be load-bearing for the one-active-term partial unique index: if it were possible
/// to reopen a term while its true successor is active, two terms would be active at once.
/// </remarks>
internal sealed class ReopenTermHandler(
    ITermRepository terms,
    IAcademicSessionRepository sessions,
    IAdminAccountRepository accounts,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<ReopenTermCommand, Result<TermDto>>
{
    /// <inheritdoc />
    public async Task<Result<TermDto>> HandleAsync(ReopenTermCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } actorIdText || !Guid.TryParse(actorIdText, out var actorId))
        {
            return Result.Failure<TermDto>(Error.Unauthenticated(
                "authentication.required",
                "Sign in to perform this action."));
        }

        var term = await terms.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (term is null)
        {
            return Result.Failure<TermDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var actor = await accounts.FindReadOnlyByIdAsync(actorId, cancellationToken).ConfigureAwait(false);

        if (actor is not { IsSuperAdmin: true })
        {
            return Result.Failure<TermDto>(Error.Forbidden(
                "term.reopen_requires_super_admin",
                "Only a Super Admin may reopen a closed term."));
        }

        bool followingTermOpened;

        if (term.Ordinal < 3)
        {
            var following = await terms
                .FindByOrdinalAsync(term.SessionId, term.Ordinal + 1, cancellationToken)
                .ConfigureAwait(false);

            followingTermOpened = following is not null && following.State != TermState.Upcoming;
        }
        else
        {
            var session = await sessions.FindReadOnlyByIdAsync(term.SessionId, cancellationToken).ConfigureAwait(false);
            followingTermOpened = session is { State: SessionState.Closed };
        }

        var canReopen = TermTransitionGuard.CanReopen(term, followingTermOpened);

        if (canReopen.IsFailure)
        {
            return Result.Failure<TermDto>(canReopen.Error);
        }

        var reopen = term.Reopen();

        if (reopen.IsFailure)
        {
            return Result.Failure<TermDto>(reopen.Error);
        }

        await auditSink.RecordAsync(
            "term.reopen",
            "term",
            term.Id.ToString("D", CultureInfo.InvariantCulture),
            new Dictionary<string, object?> { ["reason"] = request.Reason },
            currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SessionMapper.ToTermDto(term));
    }
}
