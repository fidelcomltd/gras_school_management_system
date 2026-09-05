using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Infrastructure.Authorization;

/// <summary>
/// <see cref="IEffectivePrivilegeProvider"/>, scoped to the <c>is_super_admin</c> flag-bypass path
/// only (TASK-0003, human ruling 2026-09-05 — see <c>AdminAccount</c>'s class remarks). Replaces
/// the former <c>NullEffectivePrivilegeProvider</c> (TASK-0002).
/// </summary>
/// <remarks>
/// <c>is_super_admin</c> ⇒ one <see cref="PrivilegeGrant"/> per privilege in the immutable register,
/// <see cref="ScopeType.SchoolWide"/>, <c>SessionId: null</c> (spec 6.1.7 rule 4: not part of any
/// role's privilege list, so it is resolved directly rather than through a role assignment). Every
/// other account still resolves to an empty set — no <c>role</c>/<c>role_assignment</c> table exists
/// yet; TASK-0019 owns real role assignment for non-super-admin accounts, and replaces this
/// registration again when it lands.
/// </remarks>
internal sealed class SuperAdminFlagEffectivePrivilegeProvider(IAdminAccountRepository accounts)
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
        string userId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (!Guid.TryParse(userId, out var accountId))
        {
            return NoGrants;
        }

        var account = await accounts.FindReadOnlyByIdAsync(accountId, cancellationToken).ConfigureAwait(false);

        return account is { IsSuperAdmin: true } ? SuperAdminGrants : NoGrants;
    }
}
