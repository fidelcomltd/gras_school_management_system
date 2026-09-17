using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IResultSetRepository"/>.</summary>
internal sealed class ResultSetRepository(ApplicationDbContext context) : IResultSetRepository
{
    // Spec 6.3.6's own list, literally, is Draft/Awaiting Approval/Approved. ReturnedForCorrection is
    // a DELIBERATE DEPARTURE from that literal list — TASK-0076, HUMAN RULING 2026-09-17: a returned
    // set also has marks editable (spec 6.7.11) and must also block, or it is stranded once the term
    // closes, since score entry then 409s against the closed term and the class teacher can never
    // act on the return reason.
    private static readonly ResultSetState[] BlockingStates =
    [
        ResultSetState.Draft,
        ResultSetState.AwaitingApproval,
        ResultSetState.Approved,
        ResultSetState.ReturnedForCorrection,
    ];

    /// <inheritdoc />
    public async Task<IReadOnlyList<BlockingResultSetSummary>> ListBlockingTermCloseAsync(
        Guid termId, CancellationToken cancellationToken)
    {
        // Spec 6.3.6: blocking requires marks ENTERED, not merely a result set in a blocking state —
        // "An arm with no marks entered at all does not block closure." A result set can exist with
        // no subject_score row at all (spec 6.7.11: also created by a first save of traits,
        // attendance or a remark), so the state alone is not the precondition; at least one
        // non-voided subject_score row for the set is. Checked database-side via EXISTS, not by
        // loading scores, so this stays one query regardless of how many marks a set holds.
        var rows = await (
            from resultSet in context.ResultSets.AsNoTracking()
            where resultSet.TermId == termId
                && BlockingStates.Contains(resultSet.State)
                && context.SubjectScores.AsNoTracking().Any(score =>
                    score.ResultSetId == resultSet.Id && score.VoidedAt == null)
            join arm in context.Arms.AsNoTracking() on resultSet.ArmId equals arm.Id
            join level in context.ClassLevels.AsNoTracking() on arm.ClassLevelId equals level.Id
            select new { arm.Id, arm.Label, LevelName = level.Name, resultSet.State })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(row => new BlockingResultSetSummary(row.Id, ArmDisplayName.Compose(row.LevelName, row.Label), row.State))
            .OrderBy(summary => summary.ArmDisplayName, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public Task<ResultSet?> FindTrackedByArmTermAsync(Guid armId, Guid termId, CancellationToken cancellationToken) =>
        context.ResultSets.FirstOrDefaultAsync(
            resultSet => resultSet.ArmId == armId && resultSet.TermId == termId, cancellationToken);

    /// <inheritdoc />
    public Task<ResultSet?> FindReadOnlyByArmTermAsync(Guid armId, Guid termId, CancellationToken cancellationToken) =>
        context.ResultSets.AsNoTracking().FirstOrDefaultAsync(
            resultSet => resultSet.ArmId == armId && resultSet.TermId == termId, cancellationToken);

    /// <inheritdoc />
    public Task AddAsync(ResultSet resultSet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultSet);
        cancellationToken.ThrowIfCancellationRequested();

        context.ResultSets.Add(resultSet);

        return Task.CompletedTask;
    }
}
