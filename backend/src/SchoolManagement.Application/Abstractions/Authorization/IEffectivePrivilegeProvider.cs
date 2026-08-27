namespace SchoolManagement.Application.Abstractions.Authorization;

/// <summary>
/// Resolves a caller's effective privilege set: "the union of all privileges from all active
/// assignments held by the account, each tagged with the scope it arrived through" (spec 4.2).
/// </summary>
/// <remarks>
/// <para>
/// A SEAM. TASK-0002 builds the resolution and enforcement machinery that consumes this; it does
/// not build <c>admin_account</c>/<c>role</c>/<c>role_assignment</c> persistence — that is
/// explicitly out of scope for this card. The registered implementation
/// (<c>NullEffectivePrivilegeProvider</c>) always returns an empty set, which keeps the system
/// deny-by-default until the role/assignment module implements this against real data.
/// </para>
/// <para>
/// Keyed by the caller's stable user id (<c>ICurrentUser.UserId</c>) rather than taking
/// <c>ICurrentUser</c> directly, so the authorization handler and unit tests can supply any user
/// id without constructing an HTTP context.
/// </para>
/// </remarks>
public interface IEffectivePrivilegeProvider
{
    /// <summary>Returns every active grant the given account currently holds.</summary>
    /// <param name="userId">The account's stable identifier (<c>ICurrentUser.UserId</c>).</param>
    /// <param name="cancellationToken">Propagated to any underlying lookup.</param>
    Task<IReadOnlyCollection<PrivilegeGrant>> GetGrantsAsync(string userId, CancellationToken cancellationToken);
}
