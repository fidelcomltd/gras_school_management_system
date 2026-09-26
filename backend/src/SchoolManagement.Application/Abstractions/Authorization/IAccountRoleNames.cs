namespace SchoolManagement.Application.Abstractions.Authorization;

/// <summary>
/// The names of the roles an account currently holds, for display only (the header's "name and role"). Authorization
/// never reads this: it reads <see cref="IEffectivePrivilegeProvider"/>'s grants.
/// </summary>
public interface IAccountRoleNames
{
    /// <summary>
    /// <c>["Super Admin"]</c> for a super admin; otherwise the distinct names of the roles behind the account's active
    /// assignments, ordered by name. Empty for an account with no active assignment.
    /// </summary>
    Task<IReadOnlyList<string>> GetActiveRoleNamesAsync(Guid accountId, bool isSuperAdmin, CancellationToken cancellationToken);
}
