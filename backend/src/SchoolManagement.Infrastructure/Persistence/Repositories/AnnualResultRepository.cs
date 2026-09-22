using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Results.Annual;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IAnnualResultRepository"/>.</summary>
internal sealed class AnnualResultRepository(ApplicationDbContext context) : IAnnualResultRepository
{
    private const int ThirdTerm = 3;

    public async Task<AnnualArmContext?> LoadArmAsync(Guid armId, CancellationToken cancellationToken)
    {
        var arm = await (
                from candidate in context.Arms.AsNoTracking()
                join level in context.ClassLevels.AsNoTracking() on candidate.ClassLevelId equals level.Id
                where candidate.Id == armId
                select new { candidate.Id, candidate.SessionId, LevelName = level.Name, candidate.Label })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (arm is null)
        {
            return null;
        }

        var terms = await (
                from term in context.Terms.AsNoTracking()
                where term.SessionId == arm.SessionId
                join resultSet in context.ResultSets.AsNoTracking().Where(set => set.ArmId == armId) on term.Id equals resultSet.TermId into sets
                from resultSet in sets.DefaultIfEmpty()
                orderby term.Ordinal
                select new { term.Id, term.Ordinal, term.Name, State = resultSet == null ? (ResultSetState?)null : resultSet.State, Snapshot = resultSet == null ? null : resultSet.ConfigSnapshotJson })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AnnualArmContext(
            arm.Id,
            arm.SessionId,
            arm.LevelName,
            arm.Label,
            terms.Select(term => new AnnualTermState(term.Id, term.Ordinal, term.Name, term.State)).ToList(),
            terms.FirstOrDefault(term => term.Ordinal == ThirdTerm)?.Snapshot);
    }

    public async Task<IReadOnlyList<AnnualPupilInput>> LoadPupilsAsync(Guid armId, Guid sessionId, CancellationToken cancellationToken)
    {
        // The final arm's pupils: those with a result in its Third Term set.
        var pupilIds = await (
                from result in context.PupilTermResults.AsNoTracking()
                join resultSet in context.ResultSets.AsNoTracking() on result.ResultSetId equals resultSet.Id
                join term in context.Terms.AsNoTracking() on resultSet.TermId equals term.Id
                where resultSet.ArmId == armId && term.SessionId == sessionId && term.Ordinal == ThirdTerm
                select result.PupilId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Their published term results anywhere in the session: term results travel with the pupil (6.7.10).
        var results = await (
                from result in context.PupilTermResults.AsNoTracking()
                join resultSet in context.ResultSets.AsNoTracking() on result.ResultSetId equals resultSet.Id
                join term in context.Terms.AsNoTracking() on resultSet.TermId equals term.Id
                where term.SessionId == sessionId && resultSet.State == ResultSetState.Published && pupilIds.Contains(result.PupilId)
                select new { result.PupilId, result.ResultSetId, term.Ordinal, result.Average, result.TotalObtained, resultSet.PublishedAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // A pupil moved mid-term can have two results for one term; the later publication is the one of record.
        var chosen = results
            .GroupBy(result => (result.PupilId, result.Ordinal))
            .Select(group => group.OrderByDescending(result => result.PublishedAtUtc).First())
            .ToList();
        var setIds = chosen.Select(result => result.ResultSetId).Distinct().ToList();
        var lines = await context.SubjectResultLines.AsNoTracking()
            .Where(line => setIds.Contains(line.ResultSetId) && pupilIds.Contains(line.PupilId))
            .Select(line => new { line.ResultSetId, line.PupilId, line.SubjectId, line.SubjectTotal })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var linesBySetAndPupil = lines.ToLookup(line => (line.ResultSetId, line.PupilId));

        return pupilIds.Distinct()
            .Select(pupilId => new AnnualPupilInput(
                pupilId,
                chosen.Where(result => result.PupilId == pupilId)
                    .OrderBy(result => result.Ordinal)
                    .Select(result => new AnnualTermInput(
                        result.Ordinal,
                        result.Average,
                        result.TotalObtained,
                        linesBySetAndPupil[(result.ResultSetId, pupilId)].Select(line => new AnnualSubjectTotal(line.SubjectId, line.SubjectTotal)).ToList()))
                    .ToList()))
            .ToList();
    }

    public async Task ReplaceAsync(Guid sessionId, IReadOnlyCollection<Guid> pupilIds, IReadOnlyList<AnnualResult> rows, CancellationToken cancellationToken)
    {
        // A pupil has one annual result per session, whichever arm computed it last.
        await context.AnnualResults
            .Where(result => result.SessionId == sessionId && pupilIds.Contains(result.PupilId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        context.AnnualResults.AddRange(rows);
    }
}
