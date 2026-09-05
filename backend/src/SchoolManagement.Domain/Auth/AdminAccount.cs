using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Auth;

/// <summary>
/// A back-office administrator account (spec 6.1.3). TASK-0003 builds this entity and populates
/// exactly one row, off the wire, via the bootstrap seam — see <see cref="CreateBootstrapSuperAdmin"/>.
/// Admin account CRUD (create, edit, suspend, deactivate) is TASK-0019.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IsSuperAdmin"/> is a FLAG BYPASS, not a role assignment (human ruling, 2026-09-05,
/// recorded in <c>.agent/decisions/2026-Q3-contract-deltas.md</c> § TASK-0003 §1): spec 6.1.7 rule 4
/// governs over 4.2.2's looser wording. No <c>role</c>/<c>role_assignment</c> table exists yet;
/// <c>IEffectivePrivilegeProvider</c>'s real implementation resolves every privilege in the register
/// directly from this one flag.
/// </para>
/// <para>
/// Password history is capped at <see cref="AuthPolicy.PasswordHistoryLimit"/> prior hashes (spec
/// 6.1.11) — the CURRENT hash lives in <see cref="PasswordHash"/>, not in the history list, so reuse
/// checks must test the candidate against both.
/// </para>
/// </remarks>
public sealed class AdminAccount : Entity<Guid>, IAuditableEntity
{
    private readonly List<string> _passwordHistoryHashes = [];

    private AdminAccount(
        Guid id,
        string email,
        string staffName,
        string passwordHash,
        bool isSuperAdmin,
        bool mustChangePassword)
        : base(id)
    {
        Email = email;
        StaffName = staffName;
        PasswordHash = passwordHash;
        IsSuperAdmin = isSuperAdmin;
        MustChangePassword = mustChangePassword;
        Status = AdminAccountStatus.Active;
    }

    // EF Core materialisation constructor.
    private AdminAccount()
        : base()
    {
        Email = null!;
        StaffName = null!;
        PasswordHash = null!;
    }

    /// <summary>Login identifier, stored lower-invariant (spec 6.1.3).</summary>
    public string Email { get; private set; }

    /// <summary>Two words minimum, letters/spaces/hyphens/apostrophes only, trimmed (spec 6.1.3).</summary>
    public string StaffName { get; private set; }

    /// <summary>The current Argon2id-encoded hash, cost parameters embedded (spec 9.1).</summary>
    public string PasswordHash { get; private set; }

    /// <summary>Up to <see cref="AuthPolicy.PasswordHistoryLimit"/> prior hashes, newest first (spec 6.1.11).</summary>
    public IReadOnlyList<string> PasswordHistoryHashes => _passwordHistoryHashes;

    /// <summary>True on creation and after a forced reset (spec 6.1.3). Gates every endpoint but three.</summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>Set only by bootstrap or by an existing Super Admin (spec 6.1.3). Flag bypass — see the class remarks.</summary>
    public bool IsSuperAdmin { get; private set; }

    /// <summary>Spec 6.1.10. Only <see cref="AdminAccountStatus.Active"/> may sign in.</summary>
    public AdminAccountStatus Status { get; private set; }

    /// <summary>Written on successful sign-in (spec 6.1.3).</summary>
    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    /// <summary>Consecutive failed attempts within the current <see cref="AuthPolicy.LockoutWindow"/>.</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>
    /// When the most recent failed attempt was recorded. Not part of spec 6.1.3's table — an
    /// implementation detail needed to decide whether a new failure starts a fresh window.
    /// </summary>
    public DateTimeOffset? LastFailedLoginAtUtc { get; private set; }

    /// <summary>Set by the lockout rule (spec 6.1.11). <c>null</c> or in the past means not locked.</summary>
    public DateTimeOffset? LockedUntilUtc { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>Whether the account may sign in at all, independent of password/lockout.</summary>
    public bool CanSignIn => Status == AdminAccountStatus.Active;

    /// <summary>Whether the account is locked at <paramref name="now"/> (spec 6.1.11).</summary>
    public bool IsLockedAt(DateTimeOffset now) => LockedUntilUtc is { } lockedUntil && lockedUntil > now;

    /// <summary>
    /// Creates the one account the bootstrap seam is allowed to create (spec 6.1.6) — off the wire,
    /// never from an HTTP endpoint. <paramref name="passwordHash"/> must already be Argon2id-hashed;
    /// this factory never sees the plaintext.
    /// </summary>
    public static Result<AdminAccount> CreateBootstrapSuperAdmin(
        Guid id,
        string email,
        string staffName,
        string passwordHash)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<AdminAccount>(
                Error.Validation("auth.account_id_required", "Id must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<AdminAccount>(
                Error.Validation("auth.email_required", "Email must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(staffName))
        {
            return Result.Failure<AdminAccount>(
                Error.Validation("auth.staff_name_required", "Staff name must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<AdminAccount>(
                Error.Validation("auth.password_hash_required", "Password hash must not be empty."));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (normalizedEmail.Length > AuthPolicy.EmailMaxLength)
        {
            return Result.Failure<AdminAccount>(Error.Validation(
                "auth.email_too_long",
                $"Email must be at most {AuthPolicy.EmailMaxLength} characters."));
        }

        var trimmedName = staffName.Trim();

        if (trimmedName.Length > AuthPolicy.StaffNameMaxLength)
        {
            return Result.Failure<AdminAccount>(Error.Validation(
                "auth.staff_name_too_long",
                $"Staff name must be at most {AuthPolicy.StaffNameMaxLength} characters."));
        }

        return Result.Success(new AdminAccount(
            id,
            normalizedEmail,
            trimmedName,
            passwordHash,
            isSuperAdmin: true,
            mustChangePassword: true));
    }

    /// <summary>Records a failed sign-in attempt, applying the rolling-window lockout rule (spec 6.1.11).</summary>
    public void RegisterFailedLogin(DateTimeOffset now)
    {
        if (LastFailedLoginAtUtc is null || now - LastFailedLoginAtUtc.Value > AuthPolicy.LockoutWindow)
        {
            // The prior window has closed (or this is the first-ever failure): start counting fresh.
            FailedLoginCount = 0;
        }

        FailedLoginCount++;
        LastFailedLoginAtUtc = now;

        if (FailedLoginCount >= AuthPolicy.LockoutFailureThreshold)
        {
            LockedUntilUtc = now + AuthPolicy.LockoutDuration;
        }
    }

    /// <summary>Clears lockout state and records the login timestamp (spec 6.1.3).</summary>
    public void RegisterSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        LastFailedLoginAtUtc = null;
        LockedUntilUtc = null;
        LastLoginAtUtc = now;
    }

    /// <summary>
    /// The current hash followed by every retained history entry — every encoded hash a reuse check
    /// (spec 6.1.11) must verify a candidate plaintext against.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT a hash-string equality check: Argon2id salts each hash independently, so
    /// re-hashing the same plaintext never produces the same encoded string twice. The caller must
    /// verify the candidate plaintext against each entry here via the password hasher —
    /// string-comparing encoded hashes can never detect reuse.
    /// </remarks>
    public IEnumerable<string> AllPasswordHashesForReuseCheck() =>
        _passwordHistoryHashes.Prepend(PasswordHash);

    /// <summary>
    /// Rotates to a new password hash, pushing the previous one onto the retained history (capped at
    /// <see cref="AuthPolicy.PasswordHistoryLimit"/>) and clearing <see cref="MustChangePassword"/>.
    /// </summary>
    public void ChangePassword(string newPasswordHash)
    {
        _passwordHistoryHashes.Insert(0, PasswordHash);

        while (_passwordHistoryHashes.Count > AuthPolicy.PasswordHistoryLimit)
        {
            _passwordHistoryHashes.RemoveAt(_passwordHistoryHashes.Count - 1);
        }

        PasswordHash = newPasswordHash;
        MustChangePassword = false;
    }
}
