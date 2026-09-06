namespace SchoolManagement.Domain.Security;

/// <summary>
/// Which of spec 4.4's six privilege-register groups (4.4.1 through 4.4.6) a
/// <see cref="PrivilegeDefinition"/> belongs to.
/// </summary>
/// <remarks>
/// <para>
/// Member order matches the spec's own section order. <see cref="PrivilegeRegistry.All"/> is
/// already listed 4.4.1 through 4.4.6, and LINQ's <c>GroupBy</c> preserves the source sequence's
/// order (a documented guarantee, not an implementation accident), so grouping the register by
/// this value reproduces spec order with no extra sort step.
/// </para>
/// <para>
/// This type carries no serialization concern of its own — it is a domain grouping key, nothing
/// else. The approved contract delta's wire-facing key (for example <c>academic_structure</c>) and
/// the verbatim spec 4.4.x section heading are looked up from a value of this type via
/// <see cref="PrivilegeModuleCatalog"/>, not encoded here.
/// </para>
/// </remarks>
public enum PrivilegeModule
{
    /// <summary>Spec 4.4.1, "Administration and access control".</summary>
    Administration = 0,

    /// <summary>Spec 4.4.2, "Settings".</summary>
    Settings = 1,

    /// <summary>Spec 4.4.3, "Academic structure".</summary>
    AcademicStructure = 2,

    /// <summary>Spec 4.4.4, "Pupils, guardians and subjects".</summary>
    PupilsAndSubjects = 3,

    /// <summary>Spec 4.4.5, "Results".</summary>
    Results = 4,

    /// <summary>Spec 4.4.6, "Pins and reports".</summary>
    PinsAndReports = 5,
}
