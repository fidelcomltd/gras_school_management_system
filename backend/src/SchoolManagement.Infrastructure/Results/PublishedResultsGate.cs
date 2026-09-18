using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Results;

/// <summary>
/// The real <see cref="IPublishedResultsGate"/> (TASK-0076 dispatch A): a real query against
/// <c>result_set</c>, replacing the honestly-zero stand-in that predated the table's existence (see
/// the interface's remarks for why that was correct, not a placeholder, at the time).
/// </summary>
internal sealed class PublishedResultsGate(ApplicationDbContext context) : IPublishedResultsGate
{
    /// <summary>Term.Ordinal for the third term of a session (spec 6.3.4).</summary>
    private const int ThirdTermOrdinal = 3;

    /// <inheritdoc />
    public Task<int> CountPublishedInSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        (from resultSet in context.ResultSets.AsNoTracking()
         join term in context.Terms.AsNoTracking() on resultSet.TermId equals term.Id
         where term.SessionId == sessionId && resultSet.State == ResultSetState.Published
         select resultSet.Id)
        .CountAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> AnyThirdTermPublishedInSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        (from resultSet in context.ResultSets.AsNoTracking()
         join term in context.Terms.AsNoTracking() on resultSet.TermId equals term.Id
         where term.SessionId == sessionId && term.Ordinal == ThirdTermOrdinal && resultSet.State == ResultSetState.Published
         select resultSet.Id)
        .AnyAsync(cancellationToken);
}
