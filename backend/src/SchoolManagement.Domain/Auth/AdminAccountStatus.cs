namespace SchoolManagement.Domain.Auth;

/// <summary>
/// Lifecycle state of an <see cref="AdminAccount"/> (spec 6.1.10). Only <see cref="Active"/> may
/// sign in.
/// </summary>
/// <remarks>
/// The transitions themselves (<c>admin.suspend</c>, <c>admin.deactivate</c>) are TASK-0019's
/// admin-account-management surface. This card only reads the field to decide whether sign-in may
/// proceed — no endpoint in this card changes it, and the bootstrap account is always created
/// <see cref="Active"/>.
/// </remarks>
public enum AdminAccountStatus
{
    /// <summary>Normal. May sign in.</summary>
    Active = 0,

    /// <summary>Temporary. May not sign in; existing sessions are revoked (spec 6.1.10).</summary>
    Suspended = 1,

    /// <summary>The person has left. May not sign in; assignments and sessions are revoked (spec 6.1.10).</summary>
    Deactivated = 2,
}
