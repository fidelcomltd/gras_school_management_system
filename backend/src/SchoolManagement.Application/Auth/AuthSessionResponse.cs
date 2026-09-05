using System.Globalization;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Auth;

/// <summary>
/// Shared response shape returned by <c>sign-in</c>, <c>me</c>, <c>refresh</c> and <c>password</c>
/// (approved contract delta §0), so the frontend never needs a second round trip to learn its own
/// state after any of the four.
/// </summary>
/// <param name="AccountId">Opaque identifier (root CLAUDE.md §8 — never parsed by the client).</param>
/// <param name="Email">The account's login identifier.</param>
/// <param name="StaffName">Display name.</param>
/// <param name="IsSuperAdmin">Whether the flag-bypass privilege path applies (TASK-0003 §1).</param>
/// <param name="MustChangePassword">Whether the forced-change gate currently applies to this account.</param>
/// <param name="EffectivePrivileges">
/// The caller's resolved effective privilege set. For this card, populated only via the
/// <see cref="IsSuperAdmin"/> flag path — see <c>SuperAdminFlagEffectivePrivilegeProvider</c>.
/// </param>
/// <param name="SessionExpiresAt">
/// The sooner of the session's idle and absolute deadlines, recomputed on every response.
/// </param>
/// <param name="SessionAbsoluteExpiresAt">
/// The session's fixed absolute deadline (spec 6.1.11's 8-hour cap), set once at sign-in.
/// </param>
public sealed record AuthSessionResponse(
    string AccountId,
    string Email,
    string StaffName,
    bool IsSuperAdmin,
    bool MustChangePassword,
    IReadOnlyList<EffectivePrivilegeDto> EffectivePrivileges,
    DateTimeOffset SessionExpiresAt,
    DateTimeOffset SessionAbsoluteExpiresAt)
{
    /// <summary>Builds the response from the account/session facts a handler has assembled.</summary>
    public static AuthSessionResponse Create(
        Guid accountId,
        string email,
        string staffName,
        bool isSuperAdmin,
        bool mustChangePassword,
        IReadOnlyCollection<PrivilegeGrant> grants,
        DateTimeOffset idleExpiresAtUtc,
        DateTimeOffset absoluteExpiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(grants);

        var privileges = grants
            .Select(grant => new EffectivePrivilegeDto(grant.Privilege, grant.Scope, [.. grant.ArmIds]))
            .ToArray();

        var soonerExpiry = idleExpiresAtUtc < absoluteExpiresAtUtc ? idleExpiresAtUtc : absoluteExpiresAtUtc;

        return new AuthSessionResponse(
            AccountId: accountId.ToString("D", CultureInfo.InvariantCulture),
            Email: email,
            StaffName: staffName,
            IsSuperAdmin: isSuperAdmin,
            MustChangePassword: mustChangePassword,
            EffectivePrivileges: privileges,
            SessionExpiresAt: soonerExpiry,
            SessionAbsoluteExpiresAt: absoluteExpiresAtUtc);
    }
}

/// <summary>One entry of <see cref="AuthSessionResponse.EffectivePrivileges"/>, mirroring <see cref="PrivilegeGrant"/>
/// minus its session id — that field is server-internal and never crosses the wire.</summary>
/// <param name="Privilege">The canonical privilege code.</param>
/// <param name="Scope">Whether this grant applies school-wide or over a named list of arms.</param>
/// <param name="ArmIds">The arms this grant covers. Empty when <paramref name="Scope"/> is school-wide.</param>
public sealed record EffectivePrivilegeDto(string Privilege, ScopeType Scope, IReadOnlyList<Guid> ArmIds);
