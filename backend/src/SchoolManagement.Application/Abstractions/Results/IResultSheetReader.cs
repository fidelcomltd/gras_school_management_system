using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>A subject's stored marks for one pupil.</summary>
/// <param name="SubjectId">The subject.</param>
/// <param name="ComponentMarksJson">Map of non-examination component id to mark.</param>
/// <param name="ExamMark">Null when absent or unentered.</param>
/// <param name="ExamAbsent">Prints ABS.</param>
/// <param name="Voided">A voided row prints blank.</param>
public sealed record SheetScoreRow(Guid SubjectId, string ComponentMarksJson, int? ExamMark, bool ExamAbsent, bool Voided);

/// <summary>A subject's computed line for one pupil.</summary>
/// <param name="SubjectId">The subject.</param>
/// <param name="SubjectTotal">Out of 100.</param>
/// <param name="Grade">Band letter.</param>
/// <param name="Remark">The band's word.</param>
public sealed record SheetLineRow(Guid SubjectId, int SubjectTotal, string Grade, string Remark);

/// <summary>A rating the pupil received: a trait or a development indicator, with an optional comment.</summary>
/// <param name="ItemId">The trait or indicator.</param>
/// <param name="PointId">The rating scale point.</param>
/// <param name="Comment">Nursery indicator comment, if any.</param>
public sealed record SheetRatingRow(Guid ItemId, Guid PointId, string? Comment);

/// <summary>Everything stored about one pupil in one published result set, plus the snapshot to read it against.</summary>
/// <param name="ResultSetId">The set.</param>
/// <param name="State">Only Published sets reach the portal.</param>
/// <param name="SnapshotJson">The configuration snapshot, or null before publication.</param>
/// <param name="RevisionNumber">1 on first publication.</param>
/// <param name="PublishedAt">This revision's issue date.</param>
/// <param name="PreviousPublishedAt">The replaced revision's issue date, for the revision notice.</param>
/// <param name="PupilSurname">Printed in capitals.</param>
/// <param name="PupilGivenNames">First and middle names.</param>
/// <param name="DateOfBirth">For the age at term end.</param>
/// <param name="RegistrationNumber">The pupil's current number.</param>
/// <param name="Scores">Stored marks.</param>
/// <param name="Lines">Computed subject lines.</param>
/// <param name="Average">Term average, or null if not computed.</param>
/// <param name="OverallGrade">Band letter for the average.</param>
/// <param name="TraitRatings">Primary trait ratings.</param>
/// <param name="DevelopmentRatings">Nursery indicator ratings.</param>
/// <param name="TimesPresent">Stored attendance.</param>
/// <param name="TeacherComment">Class teacher's remark.</param>
/// <param name="HeadTeacherComment">Head teacher's remark.</param>
public sealed record ResultSheetData(
    Guid ResultSetId,
    ResultSetState State,
    string? SnapshotJson,
    int RevisionNumber,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? PreviousPublishedAt,
    string PupilSurname,
    string PupilGivenNames,
    DateOnly DateOfBirth,
    string RegistrationNumber,
    IReadOnlyList<SheetScoreRow> Scores,
    IReadOnlyList<SheetLineRow> Lines,
    decimal? Average,
    string? OverallGrade,
    IReadOnlyList<SheetRatingRow> TraitRatings,
    IReadOnlyList<SheetRatingRow> DevelopmentRatings,
    int? TimesPresent,
    string? TeacherComment,
    string? HeadTeacherComment);

/// <summary>Reads everything a result sheet needs for one pupil in one arm-term, in one place.</summary>
public interface IResultSheetReader
{
    /// <summary>
    /// The pupil's sheet data for the result set of the arm they were enrolled in during <paramref name="termId"/>,
    /// or null when there is no such result set.
    /// </summary>
    Task<ResultSheetData?> ReadAsync(Guid pupilId, Guid termId, CancellationToken cancellationToken);
}
