using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Results;

/// <summary>
/// The real <see cref="ISubjectScoreSessionLockLookup"/> (TASK-0076 dispatch A): a real query against
/// <c>subject_score</c>, replacing the honestly-<see langword="false"/> stand-in that predated the
/// table's existence (see the interface's remarks for why that was correct, not a placeholder, at
/// the time).
/// </summary>
/// <remarks>
/// Counts a VOIDED mark too — spec 6.2.6's lock triggers "once the first mark is entered anywhere in
/// a session", and a mark that was later voided was still entered; unwinding a mistake does not
/// un-happen the fact that the structure was in effect when it was made.
/// </remarks>
internal sealed class SubjectScoreSessionLockLookup(ApplicationDbContext context) : ISubjectScoreSessionLockLookup
{
    /// <inheritdoc />
    public Task<bool> AnyScoreExistsInSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        (from score in context.SubjectScores.AsNoTracking()
         join resultSet in context.ResultSets.AsNoTracking() on score.ResultSetId equals resultSet.Id
         join term in context.Terms.AsNoTracking() on resultSet.TermId equals term.Id
         where term.SessionId == sessionId
         select score.Id)
        .AnyAsync(cancellationToken);
}
