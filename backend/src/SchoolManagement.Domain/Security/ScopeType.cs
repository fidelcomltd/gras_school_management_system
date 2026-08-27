namespace SchoolManagement.Domain.Security;

/// <summary>
/// How a role assignment's grant is bounded. Spec 4.2: "Scope is one of two things: school-wide,
/// or a list of specific arms in a specific session."
/// </summary>
public enum ScopeType
{
    /// <summary>The assignment grants its privileges across the whole school.</summary>
    SchoolWide = 0,

    /// <summary>The assignment grants its privileges only over a named list of arms.</summary>
    ArmList = 1,
}
