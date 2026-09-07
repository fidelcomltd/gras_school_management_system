using System.Text.RegularExpressions;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Auth;

/// <summary>
/// A back-office administrator account (spec 6.1.3). TASK-0003 built this entity and populated
/// exactly one row, off the wire, via the bootstrap seam — see <see cref="CreateBootstrapSuperAdmin"/>.
/// TASK-0027 adds the regular CRUD surface (<see cref="Create"/>, <see cref="ChangeOwnDetails"/>,
/// <see cref="UpdateDetails"/>, <see cref="SetSuperAdmin"/>, the status transitions and
/// <see cref="ForcePasswordReset"/>) — roles and assignments remain TASK-0028.
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
    // Two words minimum, letters/spaces/hyphens/apostrophes only (spec 6.1.3). Applied to input
    // already trimmed of leading/trailing whitespace by the caller below.
    private static readonly Regex StaffNamePattern = new(
        @"^[A-Za-z'\-]+(?:[ \t]+[A-Za-z'\-]+)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly List<string> _passwordHistoryHashes = [];

    private AdminAccount(
        Guid id,
        string email,
        string staffName,
        string? phone,
        string passwordHash,
        bool isSuperAdmin,
        bool mustChangePassword)
        : base(id)
    {
        Email = email;
        StaffName = staffName;
        Phone = phone;
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

    /// <summary>
    /// Nigerian format, normalised to <c>+234</c> form (spec 6.1.3). <see langword="null"/> only for
    /// the bootstrap account (<see cref="CreateBootstrapSuperAdmin"/>), which predates this field and
    /// never collected one — every account created through <see cref="Create"/> requires it.
    /// </summary>
    public string? Phone { get; private set; }

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
            phone: null,
            passwordHash,
            isSuperAdmin: true,
            mustChangePassword: true));
    }

    /// <summary>
    /// Creates a regular account through <c>POST /admins</c> (spec 6.1.9 step 1): always
    /// <see cref="AdminAccountStatus.Active"/>, <see cref="IsSuperAdmin"/> false,
    /// <see cref="MustChangePassword"/> true. <paramref name="passwordHash"/> must already be
    /// Argon2id-hashed (the generated temporary password never reaches this factory as plaintext).
    /// </summary>
    public static Result<AdminAccount> Create(
        Guid id,
        string email,
        string staffName,
        string phone,
        string passwordHash)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<AdminAccount>(
                Error.Validation("admin.account_id_required", "Id must not be empty."));
        }

        if (!TryNormalizeEmail(email, out var normalizedEmail, out var emailError))
        {
            return Result.Failure<AdminAccount>(emailError);
        }

        if (!TryNormalizeStaffName(staffName, out var trimmedName, out var staffNameError))
        {
            return Result.Failure<AdminAccount>(staffNameError);
        }

        if (!NigerianPhoneNumber.TryNormalize(phone, out var normalizedPhone))
        {
            return Result.Failure<AdminAccount>(Error.Validation(
                "admin.phone_invalid",
                "Phone must be a valid Nigerian number: 11 digits starting with 0 (for example " +
                "08012345678), or +234 followed by 10 digits."));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<AdminAccount>(
                Error.Validation("admin.password_hash_required", "Password hash must not be empty."));
        }

        return Result.Success(new AdminAccount(
            id,
            normalizedEmail,
            trimmedName,
            normalizedPhone,
            passwordHash,
            isSuperAdmin: false,
            mustChangePassword: true));
    }

    /// <summary>
    /// Self-edit carve-out (spec 6.1.2): "Edit an account's own details — <c>admin.update</c>, or the
    /// account itself for name, phone and password." NEVER changes <see cref="Email"/> — the caller
    /// must hold <c>admin.update</c> for that, enforced by <see cref="UpdateDetails"/> instead.
    /// </summary>
    public Result ChangeOwnDetails(string staffName, string phone)
    {
        if (!TryNormalizeStaffName(staffName, out var trimmedName, out var staffNameError))
        {
            return Result.Failure(staffNameError);
        }

        if (!NigerianPhoneNumber.TryNormalize(phone, out var normalizedPhone))
        {
            return Result.Failure(Error.Validation(
                "admin.phone_invalid",
                "Phone must be a valid Nigerian number: 11 digits starting with 0 (for example " +
                "08012345678), or +234 followed by 10 digits."));
        }

        StaffName = trimmedName;
        Phone = normalizedPhone;
        return Result.Success();
    }

    /// <summary>
    /// Full edit (spec 6.1.9): "Editing an account can change staff name, email and phone." Requires
    /// the caller to hold <c>admin.update</c> — enforced by the calling handler, not here. The caller
    /// must have already checked email uniqueness (spec 6.1.3) before calling this.
    /// </summary>
    public Result UpdateDetails(string staffName, string email, string phone)
    {
        if (!TryNormalizeStaffName(staffName, out var trimmedName, out var staffNameError))
        {
            return Result.Failure(staffNameError);
        }

        if (!TryNormalizeEmail(email, out var normalizedEmail, out var emailError))
        {
            return Result.Failure(emailError);
        }

        if (!NigerianPhoneNumber.TryNormalize(phone, out var normalizedPhone))
        {
            return Result.Failure(Error.Validation(
                "admin.phone_invalid",
                "Phone must be a valid Nigerian number: 11 digits starting with 0 (for example " +
                "08012345678), or +234 followed by 10 digits."));
        }

        StaffName = trimmedName;
        Email = normalizedEmail;
        Phone = normalizedPhone;
        return Result.Success();
    }

    /// <summary>
    /// Grants or revokes the Super Admin flag bypass (spec 6.1.7 rule 4). Whether the CALLER already
    /// holds it is checked by the handler before this is invoked — this method only applies the
    /// change; it is not itself the enforcement point.
    /// </summary>
    public void SetSuperAdmin(bool isSuperAdmin) => IsSuperAdmin = isSuperAdmin;

    /// <summary>
    /// Moves <see cref="AdminAccountStatus.Active"/> to <see cref="AdminAccountStatus.Suspended"/>, or
    /// back (spec 6.1.10). Session revocation on suspension is the caller's responsibility — this
    /// method only changes the field a session lookup keys off.
    /// </summary>
    public Result ChangeStatus(AdminAccountStatus target)
    {
        var allowed = (Status, target) switch
        {
            (AdminAccountStatus.Active, AdminAccountStatus.Suspended) => true,
            (AdminAccountStatus.Suspended, AdminAccountStatus.Active) => true,
            (AdminAccountStatus.Active, AdminAccountStatus.Deactivated) => true,
            (AdminAccountStatus.Suspended, AdminAccountStatus.Deactivated) => true,
            (AdminAccountStatus.Deactivated, AdminAccountStatus.Active) => true,
            _ => false,
        };

        if (!allowed)
        {
            return Result.Failure(Error.Validation(
                "admin.invalid_status_transition",
                $"An account cannot move from {Status} to {target}."));
        }

        Status = target;
        return Result.Success();
    }

    /// <summary>
    /// Forced reset (spec 6.1.11): "sets a new temporary password, displays it once, sets
    /// <c>must_change_password</c>, and revokes every active session for the account." Session
    /// revocation is the caller's responsibility; this rotates the hash (through the same
    /// history-retaining path as a self-service change) and RAISES <see cref="MustChangePassword"/>
    /// rather than clearing it — the one respect in which this differs from <see cref="ChangePassword"/>.
    /// </summary>
    public void ForcePasswordReset(string newPasswordHash)
    {
        _passwordHistoryHashes.Insert(0, PasswordHash);

        while (_passwordHistoryHashes.Count > AuthPolicy.PasswordHistoryLimit)
        {
            _passwordHistoryHashes.RemoveAt(_passwordHistoryHashes.Count - 1);
        }

        PasswordHash = newPasswordHash;
        MustChangePassword = true;
    }

    private static bool TryNormalizeEmail(string email, out string normalized, out Error error)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            normalized = string.Empty;
            error = Error.Validation("admin.email_required", "Email must not be empty.");
            return false;
        }

        var candidate = email.Trim().ToLowerInvariant();

        if (candidate.Length > AuthPolicy.EmailMaxLength)
        {
            normalized = string.Empty;
            error = Error.Validation(
                "admin.email_too_long",
                $"Email must be at most {AuthPolicy.EmailMaxLength} characters.");
            return false;
        }

        normalized = candidate;
        error = Error.None;
        return true;
    }

    private static bool TryNormalizeStaffName(string staffName, out string normalized, out Error error)
    {
        if (string.IsNullOrWhiteSpace(staffName))
        {
            normalized = string.Empty;
            error = Error.Validation("admin.staff_name_required", "Staff name must not be empty.");
            return false;
        }

        var trimmed = staffName.Trim();

        if (trimmed.Length > AuthPolicy.StaffNameMaxLength)
        {
            normalized = string.Empty;
            error = Error.Validation(
                "admin.staff_name_too_long",
                $"Staff name must be at most {AuthPolicy.StaffNameMaxLength} characters.");
            return false;
        }

        if (!StaffNamePattern.IsMatch(trimmed))
        {
            normalized = string.Empty;
            error = Error.Validation(
                "admin.staff_name_invalid",
                "Staff name must be at least two words, using only letters, spaces, hyphens and " +
                "apostrophes.");
            return false;
        }

        normalized = trimmed;
        error = Error.None;
        return true;
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
