namespace SchoolManagement.Domain.Subjects;

/// <summary>A <see cref="Subject"/>'s status (spec 6.6.2). Defaults <see cref="Active"/>.</summary>
public enum SubjectStatus
{
    /// <summary>Selectable for new mappings and new exceptions.</summary>
    Active,

    /// <summary>
    /// Cannot be newly mapped or newly excepted; stays on every mapping it already holds until that
    /// mapping ends at its own term boundary (spec 6.6.6: "deactivating a subject does not end its
    /// mappings").
    /// </summary>
    Inactive,
}
