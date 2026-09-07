namespace SchoolManagement.Domain.Security;

/// <summary>
/// Lifecycle state of a <see cref="Role"/> (spec 6.1.4). "An archived role cannot be newly assigned
/// but existing assignments continue until the session ends."
/// </summary>
public enum RoleStatus
{
    /// <summary>Normal. May be newly assigned.</summary>
    Active = 0,

    /// <summary>May not be newly assigned. Existing assignments are unaffected (TASK-0030).</summary>
    Archived = 1,
}
