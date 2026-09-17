namespace SchoolManagement.Domain.Subjects;

/// <summary>
/// How a subject reached an arm's resolved set (spec 6.6.4, <c>GET /arms/{id}/subjects</c>) — "each
/// row flagged as level-inherited or arm exception."
/// </summary>
public enum SubjectSourceKind
{
    /// <summary>Reached the arm through its level's active <see cref="SubjectMapping"/> for the term.</summary>
    LevelInherited,

    /// <summary>Reached the arm through its own <c>Include</c> <see cref="SubjectMappingException"/>.</summary>
    ArmException,
}
