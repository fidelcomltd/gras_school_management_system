namespace SchoolManagement.Domain.Auth;

/// <summary>
/// Stable codes recorded on <see cref="AdminSession.RevokedReason"/>. Not exposed over HTTP; kept as
/// named constants so a session row's history is greppable rather than a scatter of string literals.
/// </summary>
public static class AdminSessionRevocationReasons
{
    /// <summary>The account holder signed out (<c>POST /api/v1/auth/sign-out</c>).</summary>
    public const string SignOut = "sign_out";

    /// <summary>Spec 6.1.11: a password change revokes every other session for the account.</summary>
    public const string PasswordChanged = "password_changed";

    /// <summary>
    /// Spec 6.1.11: concurrent sessions are capped at <see cref="AuthPolicy.MaxConcurrentSessions"/>;
    /// the oldest is evicted to make room for a new sign-in.
    /// </summary>
    public const string SessionLimitExceeded = "session_limit_exceeded";
}
