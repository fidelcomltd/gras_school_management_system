using SchoolManagement.Application.Abstractions.Authorization;

namespace SchoolManagement.Infrastructure.Authorization;

/// <summary>
/// <see cref="IAccountRoleNames"/> over the same active assignments <see cref="RoleAssignmentEffectivePrivilegeProvider"/>
/// grants from, so the header never names a role the account's privileges do not come from.
/// </summary>
internal sealed class RoleAssignmentRoleNames(ActiveRoleAssignmentLoader loader) : IAccountRoleNames
{
    /// <summary>What a super admin is shown as: the flag, not a role assignment, is where their privileges come from.</summary>
    public const string SuperAdminName = "Super Admin";

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetActiveRoleNamesAsync(
        Guid accountId, bool isSuperAdmin, CancellationToken cancellationToken)
    {
        if (isSuperAdmin)
        {
            return [SuperAdminName];
        }

        var active = await loader.LoadAsync(accountId, cancellationToken).ConfigureAwait(false);
        var names = new SortedSet<string>(active.Select(pair => pair.Role.Name), StringComparer.OrdinalIgnoreCase);
        return [.. names];
    }
}
