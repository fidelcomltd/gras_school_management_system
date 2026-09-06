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

    /// <summary>Spec 6.1.10: suspension revokes the account's existing sessions immediately.</summary>
    public const string AccountSuspended = "account_suspended";

    /// <summary>Spec 6.1.10: deactivation revokes the account's sessions.</summary>
    public const string AccountDeactivated = "account_deactivated";

    /// <summary>Spec 6.1.11: a forced reset (<c>admin.password.reset</c>) revokes every active session.</summary>
    public const string ForcedPasswordReset = "forced_password_reset";

    /// <summary>Spec 6.1.14: <c>DELETE /admins/{id}/sessions</c> (<c>admin.session.revoke</c>).</summary>
    public const string RevokedByAdmin = "revoked_by_admin";
}
