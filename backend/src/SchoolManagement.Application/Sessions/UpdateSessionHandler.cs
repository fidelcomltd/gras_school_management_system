using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Sessions;

/// <summary>Handles <see cref="UpdateSessionCommand"/>.</summary>
internal sealed class UpdateSessionHandler(
    IAcademicSessionRepository sessions,
    ITermRepository terms,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<UpdateSessionCommand, Result<SessionDetailDto>>
{
    /// <inheritdoc />
    public async Task<Result<SessionDetailDto>> HandleAsync(UpdateSessionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await sessions.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (session is null)
        {
            return Result.Failure<SessionDetailDto>(Error.NotFound(
                "session.not_found",
                "No session was found with that id."));
        }

        // Spec 6.3.10: editable "while upcoming or active" — a closed session's name/dates are frozen.
        if (session.State == SessionState.Closed)
        {
            return Result.Failure<SessionDetailDto>(Error.Conflict(
                "session.closed_immutable",
                "A closed session's name and dates cannot be edited."));
        }

        var effectiveName = request.Name ?? session.Name;
        var effectiveStart = request.StartDate ?? session.StartDate;
        var effectiveEnd = request.EndDate ?? session.EndDate;

        if (!string.Equals(effectiveName, session.Name, StringComparison.Ordinal) &&
            await sessions.NameExistsAsync(effectiveName, session.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<SessionDetailDto>(Error.Conflict(
                "session.name_duplicate",
                "A session with that name already exists."));
        }

        var overlapping = await sessions
            .FindOverlappingAsync(effectiveStart, effectiveEnd, session.Id, cancellationToken)
            .ConfigureAwait(false);

        if (overlapping is not null)
        {
            return Result.Failure<SessionDetailDto>(Error.Validation(
                "session.overlap",
                $"{effectiveName} overlaps {overlapping.Name}, which ends on " +
                $"{overlapping.EndDate:dd/MM/yyyy}. Sessions cannot overlap."));
        }

        var reschedule = session.Reschedule(effectiveName, effectiveStart, effectiveEnd);

        if (reschedule.IsFailure)
        {
            return Result.Failure<SessionDetailDto>(reschedule.Error);
        }

        var sessionTerms = await terms.ListBySessionReadOnlyAsync(session.Id, cancellationToken).ConfigureAwait(false);

        // A narrowed session range must not orphan an already-scheduled term outside it (spec 6.3.4).
        foreach (var term in sessionTerms)
        {
            var withinSession = TermChronologyGuard.ValidateWithinSession(
                session.Name, session.StartDate, session.EndDate, term.Name, term.StartDate, term.EndDate);

            if (withinSession.IsFailure)
            {
                return Result.Failure<SessionDetailDto>(withinSession.Error);
            }
        }

        await auditSink.RecordAsync(
            Privileges.Session.Update,
            "academic_session",
            session.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SessionMapper.ToDetailDto(session, sessionTerms));
    }
}
