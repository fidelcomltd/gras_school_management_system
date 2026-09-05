namespace SchoolManagement.Domain.Auth;

/// <summary>
/// Every numeric/time constant spec 6.1.11 and spec 9.1 fix for authentication and sessions, in one
/// place so a validator, an entity and a test can never quote three different numbers for the same
/// rule.
/// </summary>
/// <remarks>
/// These are PRODUCT rules fixed by the spec, not deployment knobs — spec 6.1.11's lockout window,
/// idle timeout and concurrent-session cap are not configuration, so they are constants rather than
/// an options class. TASK-0003's contract delta cites each of these by name; changing one is a
/// contract change, not a configuration change.
/// </remarks>
public static class AuthPolicy
{
    /// <summary>Failed attempts within <see cref="LockoutWindow"/> that trigger a lockout (spec 6.1.11).</summary>
    public const int LockoutFailureThreshold = 5;

    /// <summary>The rolling window failed attempts are counted within (spec 6.1.11).</summary>
    public static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    /// <summary>How long an account stays locked once <see cref="LockoutFailureThreshold"/> is reached.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>Session idle timeout (spec 6.1.11). Extended only by a proactive <c>POST /auth/refresh</c>.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    /// <summary>Session absolute timeout (spec 6.1.11) — fixed at sign-in, never extended.</summary>
    public static readonly TimeSpan AbsoluteTimeout = TimeSpan.FromHours(8);

    /// <summary>Maximum concurrent sessions per account; the oldest is evicted (spec 6.1.11).</summary>
    public const int MaxConcurrentSessions = 3;

    /// <summary>Prior password hashes retained per account for reuse rejection (spec 6.1.11).</summary>
    public const int PasswordHistoryLimit = 5;

    /// <summary>Minimum password length (spec 6.1.11).</summary>
    public const int PasswordMinLength = 12;

    /// <summary>
    /// Maximum password length. Spec 6.1.11: "No maximum below 128" — 128 is the tightest cap that
    /// still satisfies that rule.
    /// </summary>
    public const int PasswordMaxLength = 128;

    /// <summary>Maximum staff name length (spec 6.1.3).</summary>
    public const int StaffNameMaxLength = 120;

    /// <summary>Maximum email length. Not fixed by spec 6.1.3 beyond "required"; sized generously.</summary>
    public const int EmailMaxLength = 180;

    /// <summary>Maximum length of the stored Argon2id encoded hash string.</summary>
    public const int PasswordHashMaxLength = 512;
}
