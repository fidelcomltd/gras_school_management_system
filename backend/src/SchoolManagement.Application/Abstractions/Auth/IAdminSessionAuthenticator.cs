namespace SchoolManagement.Application.Abstractions.Auth;

/// <summary>
/// Validates a raw session token on the hot path of EVERY authenticated request. Implemented in
/// Infrastructure and called directly by the Api layer's cookie authentication handler — deliberately
/// NOT a mediator <c>IQuery</c>, because it runs once per request before routing/authorization even
/// exist, not in response to a dispatched command.
/// </summary>
/// <remarks>
/// The three-way outcome (<see cref="AdminSessionAuthenticationOutcome"/>) is what lets the
/// authentication handler distinguish the three 401 variants the approved contract delta requires:
/// a token matching no row at all, one whose row is revoked, and one whose row is merely expired.
/// </remarks>
public interface IAdminSessionAuthenticator
{
    /// <summary>
    /// Looks up the session by the SHA-256 hash of <paramref name="rawSessionToken"/> and reports why
    /// it is or is not currently valid. Never writes anything — a plain read.
    /// </summary>
    Task<AdminSessionAuthenticationResult> AuthenticateAsync(
        string rawSessionToken,
        CancellationToken cancellationToken);
}

/// <summary>Why a session token did or did not authenticate.</summary>
public enum AdminSessionAuthenticationOutcome
{
    /// <summary>No session row matches the token's hash at all — maps to <c>authentication.required</c>.</summary>
    NotFound = 0,

    /// <summary>The row exists and is explicitly revoked — maps to <c>authentication.session_revoked</c>.</summary>
    Revoked = 1,

    /// <summary>The row exists, is not revoked, but its idle or absolute deadline has passed — maps to <c>authentication.session_expired</c>.</summary>
    Expired = 2,

    /// <summary>The session is live. The remaining fields are populated.</summary>
    Valid = 3,
}

/// <summary>Outcome of validating a session token, with the caller's identity when valid.</summary>
public sealed record AdminSessionAuthenticationResult(
    AdminSessionAuthenticationOutcome Outcome,
    Guid SessionId = default,
    Guid AccountId = default,
    string? Email = null,
    string? StaffName = null,
    bool IsSuperAdmin = false,
    bool MustChangePassword = false)
{
    /// <summary>Shared instance for <see cref="AdminSessionAuthenticationOutcome.NotFound"/>.</summary>
    public static AdminSessionAuthenticationResult NotFound { get; } =
        new(AdminSessionAuthenticationOutcome.NotFound);

    /// <summary>Shared instance for <see cref="AdminSessionAuthenticationOutcome.Revoked"/>.</summary>
    public static AdminSessionAuthenticationResult Revoked { get; } =
        new(AdminSessionAuthenticationOutcome.Revoked);

    /// <summary>Shared instance for <see cref="AdminSessionAuthenticationOutcome.Expired"/>.</summary>
    public static AdminSessionAuthenticationResult Expired { get; } =
        new(AdminSessionAuthenticationOutcome.Expired);
}
