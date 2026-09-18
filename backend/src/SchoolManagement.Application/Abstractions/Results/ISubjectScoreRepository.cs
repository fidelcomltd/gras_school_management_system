using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>
/// One active (non-voided) mark row, read-only, for the score-sheet GET and for computing the sheet's
/// current derived version before a save (TASK-0076 dispatch B — see <c>ScoreSheetVersion</c>).
/// </summary>
/// <param name="PupilId">The pupil this row belongs to.</param>
/// <param name="ComponentMarksJson">Raw JSON text — a map from component id to an integer mark.</param>
/// <param name="ExamMark">Null unless a mark was entered.</param>
/// <param name="ExamAbsent">True when the pupil did not sit the examination.</param>
public sealed record ScoreSheetRowSnapshot(
    Guid PupilId, string ComponentMarksJson, int? ExamMark, bool ExamAbsent);

/// <summary>
/// One active (non-voided) mark row across EVERY subject in a result set, read-only (TASK-0071's
/// computation engine — unlike <see cref="ScoreSheetRowSnapshot"/>, which is already scoped to one
/// subject by its caller's route, this reads the whole set in one query).
/// </summary>
/// <param name="PupilId">The pupil this row belongs to.</param>
/// <param name="SubjectId">The subject this row belongs to.</param>
/// <param name="ComponentMarksJson">Raw JSON text — a map from component id to an integer mark.</param>
/// <param name="ExamMark">Null unless a mark was entered.</param>
/// <param name="ExamAbsent">True when the pupil did not sit the examination.</param>
public sealed record ResultSetMarkSnapshot(
    Guid PupilId, Guid SubjectId, string ComponentMarksJson, int? ExamMark, bool ExamAbsent);

/// <summary>Persistence port for <see cref="SubjectScore"/> (TASK-0076 dispatch B).</summary>
public interface ISubjectScoreRepository
{
    /// <summary>
    /// Every active (non-voided) mark for <paramref name="resultSetId"/>/<paramref name="subjectId"/>,
    /// read-only. <c>AsNoTracking</c>.
    /// </summary>
    Task<IReadOnlyList<ScoreSheetRowSnapshot>> ListActiveReadOnlyAsync(
        Guid resultSetId, Guid subjectId, CancellationToken cancellationToken);

    /// <summary>
    /// Every active (non-voided) mark for <paramref name="resultSetId"/>, across every subject
    /// (TASK-0071: the computation engine reads a whole arm's marks in one pass, not one subject at a
    /// time). <c>AsNoTracking</c>.
    /// </summary>
    Task<IReadOnlyList<ResultSetMarkSnapshot>> ListAllActiveReadOnlyAsync(
        Guid resultSetId, CancellationToken cancellationToken);

    /// <summary>
    /// Every active (non-voided) mark for <paramref name="resultSetId"/>/<paramref name="subjectId"/>,
    /// TRACKED, for a command that will update or remove some and add others.
    /// </summary>
    Task<IReadOnlyList<SubjectScore>> ListActiveTrackedAsync(
        Guid resultSetId, Guid subjectId, CancellationToken cancellationToken);

    /// <summary>Stages a brand-new mark row for insertion. Does NOT commit.</summary>
    Task AddAsync(SubjectScore score, CancellationToken cancellationToken);

    /// <summary>
    /// Removes <paramref name="score"/> permanently — spec 6.7.4's "a row with every cell blank and
    /// not absent is not stored", so a row that becomes entirely blank is deleted rather than kept as
    /// an empty record. Does NOT commit.
    /// </summary>
    Task RemoveAsync(SubjectScore score, CancellationToken cancellationToken);
}
