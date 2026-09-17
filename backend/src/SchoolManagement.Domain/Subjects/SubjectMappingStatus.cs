namespace SchoolManagement.Domain.Subjects;

/// <summary>A <see cref="SubjectMapping"/>'s status (spec 6.6.3). Defaults <see cref="Active"/>.</summary>
public enum SubjectMappingStatus
{
    /// <summary>In effect for the term it names.</summary>
    Active,

    /// <summary>
    /// No longer in effect. A mapping is never deleted once saved — ending it is how the grid save
    /// (spec 6.6.5) and the prefill/copy actions record "this subject stopped applying," leaving the
    /// row as history rather than erasing it.
    /// </summary>
    Ended,
}
