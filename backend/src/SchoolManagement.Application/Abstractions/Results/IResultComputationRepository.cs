using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>
/// Persistence port for the three computed tables (spec 09 §6.7.6; TASK-0071):
/// <see cref="SubjectResultLine"/>, <see cref="SubjectArmStatistic"/>, <see cref="PupilTermResult"/>.
/// </summary>
public interface IResultComputationRepository
{
    /// <summary>
    /// Spec 8.2 step 11, one call: deletes every existing computed row for
    /// <paramref name="resultSetId"/> across all three tables, then stages the new rows for
    /// insertion. Does NOT commit — the unit-of-work behaviour does that inside the SAME transaction
    /// as <see cref="ResultSet.MarkComputed"/>, so a caller crash between delete and insert can never
    /// leave the tables half-written.
    /// </summary>
    Task ReplaceComputedRowsAsync(
        Guid resultSetId,
        IReadOnlyList<SubjectResultLine> subjectLines,
        IReadOnlyList<SubjectArmStatistic> subjectStatistics,
        IReadOnlyList<PupilTermResult> pupilResults,
        CancellationToken cancellationToken);
}
