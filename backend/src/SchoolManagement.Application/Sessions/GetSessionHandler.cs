using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Sessions;

/// <summary>Handles <see cref="GetSessionQuery"/>.</summary>
internal sealed class GetSessionHandler(IAcademicSessionRepository sessions, ITermRepository terms, IArmRepository arms)
    : IRequestHandler<GetSessionQuery, Result<SessionDetailDto>>
{
    /// <inheritdoc />
    public async Task<Result<SessionDetailDto>> HandleAsync(GetSessionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await sessions.FindReadOnlyByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (session is null)
        {
            return Result.Failure<SessionDetailDto>(Error.NotFound(
                "session.not_found",
                "No session was found with that id."));
        }

        var sessionTerms = await terms.ListBySessionReadOnlyAsync(session.Id, cancellationToken).ConfigureAwait(false);
        var armCount = await arms.CountBySessionAsync(session.Id, cancellationToken).ConfigureAwait(false);

        return Result.Success(SessionMapper.ToDetailDto(session, sessionTerms, armCount));
    }
}
