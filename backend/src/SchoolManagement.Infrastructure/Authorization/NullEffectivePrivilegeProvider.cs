using SchoolManagement.Application.Abstractions.Authorization;

namespace SchoolManagement.Infrastructure.Authorization;

/// <summary>
/// Default <see cref="IEffectivePrivilegeProvider"/>: every account holds no grants at all.
/// </summary>
/// <remarks>
/// TASK-0002 SEAM. <c>admin_account</c>/<c>role</c>/<c>role_assignment</c> persistence is out of
/// scope for this card (see the task card), so there is no real assignment data to resolve yet.
/// Returning an empty set rather than throwing keeps the deny-by-default guarantee: an
/// authenticated caller with zero grants fails every privilege check, exactly as an account that
/// genuinely holds nothing would. Replace this registration with a real provider when the role and
/// assignment module lands.
/// </remarks>
internal sealed class NullEffectivePrivilegeProvider : IEffectivePrivilegeProvider
{
    /// <inheritdoc />
    public Task<IReadOnlyCollection<PrivilegeGrant>> GetGrantsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return Task.FromResult<IReadOnlyCollection<PrivilegeGrant>>([]);
    }
}
