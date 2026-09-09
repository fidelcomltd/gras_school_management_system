namespace SchoolManagement.Domain.Security;

/// <summary>Lifecycle of a <see cref="RoleAssignment"/> (spec 6.1.5).</summary>
public enum RoleAssignmentStatus
{
    /// <summary>Currently in effect — contributes to the account's effective privilege set.</summary>
    Active = 0,

    /// <summary>
    /// No longer in effect. Never deleted: the row is retained because <c>audit_event</c> and other
    /// assignments' <c>granted_by</c> may reference it (spec 6.1.10's admin-account retention rule,
    /// applied the same way here).
    /// </summary>
    Revoked = 1,
}
