using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Infrastructure.Authorization;

/// <summary>
/// The graduated <see cref="IEffectivePrivilegeProvider"/> (TASK-0030): resolves a caller's effective
/// privilege set from real <c>role_assignment</c> rows, replacing
/// <c>SuperAdminFlagEffectivePrivilegeProvider</c> — DELETED by this card, not left registered behind
/// a flag (see the task card's own framing: "the project's authorization story is a placeholder"
/// until this lands).
/// </summary>
/// <remarks>
/// <para>
/// <c>is_super_admin</c> STAYS a flag bypass (human ruling 2026-09-05, spec 6.1.7 rule 4 governing
/// over 4.2.2's looser wording — see <c>AdminAccount</c>'s remarks): a super admin still resolves
/// every privilege in the register directly from the flag, exactly as the deleted provider did,
/// because <c>is_super_admin</c> "is not part of any role's privilege list" and
/// <c>CreateRoleAssignmentHandler</c> refuses to let the seeded Super Admin role be assigned through
/// this table. Every OTHER account resolves the union of privileges from its own ACTIVE assignments
/// (spec 4.2: "the union of all privileges from all active assignments held by the account, each
/// tagged with the scope it arrived through"), regardless of whether the granting role is currently
/// active or archived — 6.1.4's "existing assignments continue until the session ends" for an
/// archived role, and the lifecycle cascade for a closed session or a deleted arm, is TASK-0046
/// (spec 6.1.13; the task card's own out-of-scope list).
/// </para>
/// <para>
/// Roles are looked up one at a time rather than through a new bulk-fetch repository method: an
/// account's own distinct role count is small (assignment counts are admin-configuration-sized, the
/// same reasoning <c>IArmRepository</c>'s own remarks give), and adding a method used from exactly one
/// call site is not worth the extra surface.
/// </para>
/// </remarks>
internal sealed class RoleAssignmentEffectivePrivilegeProvider(
    IAdminAccountRepository accounts,
    IRoleAssignmentRepository assignments,
    IRoleRepository roles)
    : IEffectivePrivilegeProvider
{
    private static readonly IReadOnlyCollection<PrivilegeGrant> NoGrants = [];

    private static readonly IReadOnlyCollection<PrivilegeGrant> SuperAdminGrants =
    [
        .. PrivilegeRegistry.All.Select(definition =>
            new PrivilegeGrant(definition.Code, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null)),
    ];

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<PrivilegeGrant>> GetGrantsAsync(
        string userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (!Guid.TryParse(userId, out var accountId))
        {
            return NoGrants;
        }

        var account = await accounts.FindReadOnlyByIdAsync(accountId, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            return NoGrants;
        }

        if (account.IsSuperAdmin)
        {
            return SuperAdminGrants;
        }

        var activeAssignments = await assignments
            .ListActiveForAccountReadOnlyAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        if (activeAssignments.Count == 0)
        {
            return NoGrants;
        }

        var grants = new List<PrivilegeGrant>();
        var roleCache = new Dictionary<Guid, Role?>();

        foreach (var assignment in activeAssignments)
        {
            if (!roleCache.TryGetValue(assignment.RoleId, out var role))
            {
                role = await roles.FindReadOnlyByIdAsync(assignment.RoleId, cancellationToken).ConfigureAwait(false);
                roleCache[assignment.RoleId] = role;
            }

            if (role is null)
            {
                continue;
            }

            var armIds = new HashSet<Guid>(assignment.ArmIds);

            foreach (var privilege in role.Privileges)
            {
                grants.Add(new PrivilegeGrant(privilege, assignment.ScopeType, armIds, assignment.SessionId));
            }
        }

        return grants;
    }
}
