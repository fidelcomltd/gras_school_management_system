using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Sessions;

/// <summary>Handles <see cref="CreateSessionCommand"/>.</summary>
internal sealed class CreateSessionHandler(
    IAcademicSessionRepository sessions,
    ITermRepository terms,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<CreateSessionCommand, Result<SessionDetailDto>>
{
    private const string EntityType = "academic_session";

    private static readonly string[] TermNames = ["First Term", "Second Term", "Third Term"];

    /// <inheritdoc />
    public async Task<Result<SessionDetailDto>> HandleAsync(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await sessions.NameExistsAsync(request.Name, excludingId: null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<SessionDetailDto>(Error.Conflict(
                "session.name_duplicate",
                "A session with that name already exists."));
        }

        var creation = AcademicSession.Create(Guid.CreateVersion7(), request.Name, request.StartDate, request.EndDate);

        if (creation.IsFailure)
        {
            return Result.Failure<SessionDetailDto>(creation.Error);
        }

        var session = creation.Value;

        var overlapping = await sessions
            .FindOverlappingAsync(session.StartDate, session.EndDate, excludingId: null, cancellationToken)
            .ConfigureAwait(false);

        if (overlapping is not null)
        {
            return Result.Failure<SessionDetailDto>(Error.Validation(
                "session.overlap",
                $"{session.Name} overlaps {overlapping.Name}, which ends on " +
                $"{overlapping.EndDate:dd/MM/yyyy}. Sessions cannot overlap."));
        }

        var termInputs = new[] { request.Term1, request.Term2, request.Term3 };
        var termEntities = new List<Term>(termInputs.Length);

        for (var index = 0; index < termInputs.Length; index++)
        {
            var ordinal = index + 1;
            var name = TermNames[index];
            var input = termInputs[index];

            var withinSession = TermChronologyGuard.ValidateWithinSession(
                session.Name, session.StartDate, session.EndDate, name, input.StartDate, input.EndDate);

            if (withinSession.IsFailure)
            {
                return Result.Failure<SessionDetailDto>(withinSession.Error);
            }

            if (index > 0)
            {
                var sequential = TermChronologyGuard.ValidateSequential(
                    TermNames[index - 1], termInputs[index - 1].EndDate, name, input.StartDate);

                if (sequential.IsFailure)
                {
                    return Result.Failure<SessionDetailDto>(sequential.Error);
                }
            }

            var termCreation = Term.Create(
                Guid.CreateVersion7(), session.Id, ordinal, name, input.StartDate, input.EndDate, input.NextResumptionDate);

            if (termCreation.IsFailure)
            {
                return Result.Failure<SessionDetailDto>(termCreation.Error);
            }

            termEntities.Add(termCreation.Value);
        }

        // Single transaction by construction (AGENTS.md §4 / UnitOfWorkBehavior): this handler is one
        // ICommand, so every AddAsync below commits together or not at all — spec 6.3.5's "session
        // with two terms is not a state the school ever wants" cannot happen from a partial failure.
        await sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);

        foreach (var term in termEntities)
        {
            await terms.AddAsync(term, cancellationToken).ConfigureAwait(false);
        }

        await auditSink.RecordAsync(
            Privileges.Session.Create,
            EntityType,
            session.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SessionMapper.ToDetailDto(session, termEntities));
    }
}
