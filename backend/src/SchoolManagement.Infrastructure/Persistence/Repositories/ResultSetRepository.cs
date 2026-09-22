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
    public Task<ResultSet?> FindTrackedByIdAsync(Guid resultSetId, CancellationToken cancellationToken) =>
        context.ResultSets.FirstOrDefaultAsync(resultSet => resultSet.Id == resultSetId, cancellationToken);

    /// <inheritdoc />
    public Task AddSnapshotAsync(ResultSetSnapshot snapshot, CancellationToken cancellationToken)
    {
        context.ResultSetSnapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task AddAsync(ResultSet resultSet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultSet);
        cancellationToken.ThrowIfCancellationRequested();

        context.ResultSets.Add(resultSet);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<ResultSet?> FindTrackedByArmTermForUpdateAsync(Guid armId, Guid termId, CancellationToken cancellationToken)
    {
        // Raw SQL, same technique as AdminAccountRepository.LockActiveSuperAdminIdsAsync: the lock is
        // taken on the id(s) first, then the tracked entity is loaded through EF's own query so change
        // tracking sees it. A single arm/term pair has at most one row (the unique index), so this
        // never needs an ORDER BY of its own.
        var lockedIds = await context.Database
            .SqlQuery<Guid>(
                $"""
                SELECT id FROM result_set
                WHERE arm_id = {armId} AND term_id = {termId}
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (lockedIds.Count == 0)
        {
            return null;
        }

        return await context.ResultSets
            .FirstOrDefaultAsync(resultSet => resultSet.Id == lockedIds[0], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ResultSet?> FindTrackedByIdForUpdateAsync(Guid resultSetId, CancellationToken cancellationToken)
    {
        var lockedIds = await context.Database
            .SqlQuery<Guid>(
                $"""
                SELECT id FROM result_set
                WHERE id = {resultSetId}
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (lockedIds.Count == 0)
        {
            return null;
        }

        return await context.ResultSets
            .FirstOrDefaultAsync(resultSet => resultSet.Id == resultSetId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ResultSet>> LockNonPublishedInSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var lockedIds = await context.Database
            .SqlQuery<Guid>(
                $"""
                SELECT rs.id FROM result_set rs
                JOIN terms t ON t.id = rs.term_id
                WHERE t.session_id = {sessionId} AND rs.state <> 'Published'
                ORDER BY rs.id
                FOR UPDATE OF rs
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await LoadTrackedInLockOrderAsync(lockedIds, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ResultSet>> LockNonPublishedByTermAndClassLevelsAsync(
        Guid termId, IReadOnlyCollection<Guid> classLevelIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(classLevelIds);

        if (classLevelIds.Count == 0)
        {
            return [];
        }

        var classLevelIdArray = classLevelIds.ToArray();

        var lockedIds = await context.Database
            .SqlQuery<Guid>(
                $"""
                SELECT rs.id FROM result_set rs
                JOIN arms a ON a.id = rs.arm_id
                WHERE rs.term_id = {termId} AND a.class_level_id = ANY({classLevelIdArray}) AND rs.state <> 'Published'
                ORDER BY rs.id
                FOR UPDATE OF rs
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await LoadTrackedInLockOrderAsync(lockedIds, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads the TRACKED entities for a set of ids already row-locked by the caller's raw SQL, and
    /// returns them in the SAME order the ids were locked (ascending id) — the ordering the lock
    /// itself was taken in, not whatever order the follow-up <c>WHERE id IN (...)</c> query happens to
    /// return.
    /// </summary>
    private async Task<IReadOnlyList<ResultSet>> LoadTrackedInLockOrderAsync(
        List<Guid> lockedIdsInOrder, CancellationToken cancellationToken)
    {
        if (lockedIdsInOrder.Count == 0)
        {
            return [];
        }

        var resultSets = await context.ResultSets
            .Where(resultSet => lockedIdsInOrder.Contains(resultSet.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byId = resultSets.ToDictionary(resultSet => resultSet.Id);

        return lockedIdsInOrder.Select(id => byId[id]).ToList();
    }
}
