using SchoolManagement.Domain.Auth;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>
/// One row of <c>GET /api/v1/admins</c> (spec 6.1.8). Deliberately omits <c>rolesHeld</c> and
/// <c>scopeSummary</c> — the approved delta (`decisions/2026-Q3-contract-deltas.md`, entry
/// `TASK-0019/0027`, B4) splits roles and assignments to TASK-0028, which adds both fields
/// additively once a <c>role_assignment</c> table exists to compute them from.
/// </summary>
/// <param name="Id">Opaque identifier.</param>
/// <param name="StaffName">Display name.</param>
/// <param name="Email">Login identifier.</param>
/// <param name="Phone"><see langword="null"/> only for the pre-existing bootstrap account.</param>
/// <param name="Status">Active, suspended or deactivated (spec 6.1.10).</param>
/// <param name="IsSuperAdmin">Whether the flag-bypass privilege path applies (spec 6.1.7 rule 4).</param>
/// <param name="MustChangePassword">Whether the forced-change gate currently applies.</param>
/// <param name="LastLoginAtUtc"><see langword="null"/> if the account has never signed in.</param>
/// <param name="CreatedAtUtc">When the account was created.</param>
public sealed record AdminAccountSummaryDto(
    string Id,
    string StaffName,
    string Email,
    string? Phone,
    AdminAccountStatus Status,
    bool IsSuperAdmin,
    bool MustChangePassword,
    DateTimeOffset? LastLoginAtUtc,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// <c>GET /api/v1/admins/{id}</c> (spec 6.1.8). Deliberately omits assignments, the resolved
/// effective privilege set and the last ten audit events by this account — the approved delta (B4)
/// splits these to TASK-0028 along with roles and assignments themselves; adding them later is
/// additive.
/// </summary>
/// <param name="Id">Opaque identifier.</param>
/// <param name="StaffName">Display name.</param>
/// <param name="Email">Login identifier.</param>
/// <param name="Phone"><see langword="null"/> only for the pre-existing bootstrap account.</param>
/// <param name="Status">Active, suspended or deactivated (spec 6.1.10).</param>
/// <param name="IsSuperAdmin">Whether the flag-bypass privilege path applies (spec 6.1.7 rule 4).</param>
/// <param name="MustChangePassword">Whether the forced-change gate currently applies.</param>
/// <param name="LastLoginAtUtc"><see langword="null"/> if the account has never signed in.</param>
/// <param name="CreatedAtUtc">When the account was created.</param>
public sealed record AdminAccountDetailDto(
    string Id,
    string StaffName,
    string Email,
    string? Phone,
    AdminAccountStatus Status,
    bool IsSuperAdmin,
    bool MustChangePassword,
    DateTimeOffset? LastLoginAtUtc,
    DateTimeOffset CreatedAtUtc);

/// <summary>Maps an <see cref="AdminAccount"/> to its wire DTOs. One place, so the two shapes cannot drift.</summary>
internal static class AdminAccountMapper
{
    /// <summary>Projects to the list-row shape.</summary>
    public static AdminAccountSummaryDto ToSummaryDto(AdminAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new AdminAccountSummaryDto(
            account.Id.ToString("D", System.Globalization.CultureInfo.InvariantCulture),
            account.StaffName,
            account.Email,
            account.Phone,
            account.Status,
            account.IsSuperAdmin,
            account.MustChangePassword,
            account.LastLoginAtUtc,
            account.CreatedAtUtc);
    }

    /// <summary>Projects to the detail shape.</summary>
    public static AdminAccountDetailDto ToDetailDto(AdminAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new AdminAccountDetailDto(
            account.Id.ToString("D", System.Globalization.CultureInfo.InvariantCulture),
            account.StaffName,
            account.Email,
            account.Phone,
            account.Status,
            account.IsSuperAdmin,
            account.MustChangePassword,
            account.LastLoginAtUtc,
            account.CreatedAtUtc);
    }
}
