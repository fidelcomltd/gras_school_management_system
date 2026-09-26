using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Security;

namespace SchoolManagement.Infrastructure.Authorization;

/// <summary>
/// <see cref="IAccountRoleNames"/> over the same active assignments <see cref="RoleAssignmentEffectivePrivilegeProvider"/>
/// grants from, so the header never names a role the account's privileges do not come from.
/// </summary>
internal sealed class RoleAssignmentRoleNames(IRoleAssignmentRepository assignments, IRoleRepository roles) : IAccountRoleNames
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

        var active = await assignments.ListActiveForAccountReadOnlyAsync(accountId, cancellationToken).ConfigureAwait(false);
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleId in active.Select(assignment => assignment.RoleId).Distinct())
        {
            if (await roles.FindReadOnlyByIdAsync(roleId, cancellationToken).ConfigureAwait(false) is { } role)
            {
                names.Add(role.Name);
            }
        }

        return [.. names];
    }
}
