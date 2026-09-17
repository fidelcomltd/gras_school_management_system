namespace SchoolManagement.Domain.Subjects;

/// <summary>A <see cref="SubjectMappingException"/>'s mode (spec 6.6.4).</summary>
public enum SubjectExceptionMode
{
    /// <summary>Adds a subject the level does not take.</summary>
    Include,

    /// <summary>Removes a subject the level does take.</summary>
    Exclude,
}
