using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Results;

/// <summary>
/// The real <see cref="ISubjectMappingMarkLookup"/> (TASK-0076 dispatch A): a real query against
/// <c>subject_score</c>, replacing the honestly-empty stand-in that predated the table's existence
/// (see the interface's remarks for why that was correct, not a placeholder, at the time).
/// </summary>
/// <remarks>
/// An arm is found via <c>subject_score.result_set_id -&gt; result_set.arm_id</c> — the arm the mark
/// was entered against — never via the pupil's CURRENT open enrolment, which can have moved on since
/// (a transferred pupil's old marks must still count against the arm that entered them). Voided marks
/// are excluded: a void unwinds the entry spec 6.6.6 is trying to protect against ending a mapping
/// underneath, so a fully-voided subject no longer blocks it.
/// </remarks>
internal sealed class SubjectMappingMarkLookup(ApplicationDbContext context) : ISubjectMappingMarkLookup
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SubjectMarkArmSummary>> FindArmsWithMarksAsync(
        Guid subjectId, Guid classLevelId, Guid termId, CancellationToken cancellationToken)
    {
        var rows = await (
            from score in context.SubjectScores.AsNoTracking()
            where score.SubjectId == subjectId && score.TermId == termId && score.VoidedAt == null
            join resultSet in context.ResultSets.AsNoTracking() on score.ResultSetId equals resultSet.Id
            join arm in context.Arms.AsNoTracking() on resultSet.ArmId equals arm.Id
            where arm.ClassLevelId == classLevelId
            group score.PupilId by new { arm.Id, arm.Label } into armGroup
            select new { armGroup.Key.Id, armGroup.Key.Label, PupilCount = armGroup.Distinct().Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return [];
        }

        // A FK RESTRICT on subject_mapping.class_level_id makes an orphaned level impossible in
        // practice; the fallback only guards ArmDisplayName.Compose against a blank name.
        var levelName = await context.ClassLevels.AsNoTracking()
            .Where(level => level.Id == classLevelId)
            .Select(level => level.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? "Unknown level";

        return rows
            .Select(row => new SubjectMarkArmSummary(row.Id, ArmDisplayName.Compose(levelName, row.Label), row.PupilCount))
            .ToList();
    }
}
