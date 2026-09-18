using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// One row of an arm's resolved subject set (spec 6.6.1, 6.6.9: "the resolved set in effect for one
/// arm, with each row flagged as level-inherited or arm exception").
/// </summary>
/// <param name="SubjectId">Opaque identifier.</param>
/// <param name="SubjectName">The subject's name.</param>
/// <param name="SubjectCode"><see langword="null"/> when the subject carries none.</param>
/// <param name="DisplayOrder">
/// The row order of the result sheet (spec 6.6.1, 6.6.3) — additive beyond §6.6.9's literal text,
/// needed by the result renderer (TASK-0071) for a deterministic order without a second query.
/// </param>
/// <param name="Source">Whether this row reached the arm through its level's mapping or its own exception.</param>
public sealed record ArmSubjectDto(
    string SubjectId,
    string SubjectName,
    string? SubjectCode,
    int DisplayOrder,
    SubjectSourceKind Source);
