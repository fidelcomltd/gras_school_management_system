using SchoolManagement.Domain.Auth;

namespace SchoolManagement.UnitTests.Domain.Auth;

/// <summary>Tests spec 6.1.11's lockout rule and the password-history bookkeeping on <see cref="AdminAccount"/>.</summary>
public sealed class AdminAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateBootstrapSuperAdmin_NormalizesEmailAndTrimsStaffName()
    {
        var result = AdminAccount.CreateBootstrapSuperAdmin(
            Guid.CreateVersion7(), "  Admin@Example.COM ", "  Ada Lovelace  ", "some-hash");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe("admin@example.com");
        result.Value.StaffName.ShouldBe("Ada Lovelace");
        result.Value.MustChangePassword.ShouldBeTrue();
        result.Value.IsSuperAdmin.ShouldBeTrue();
        result.Value.CanSignIn.ShouldBeTrue();
    }

    [Fact]
    public void CreateBootstrapSuperAdmin_RejectsAnEmptyId()
    {
        var result = AdminAccount.CreateBootstrapSuperAdmin(Guid.Empty, "a@b.com", "Two Words", "hash");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("auth.account_id_required");
    }

    [Fact]
    public void RegisterFailedLogin_BelowThreshold_DoesNotLock()
    {
        var account = CreateAccount();

        for (var i = 0; i < AuthPolicy.LockoutFailureThreshold - 1; i++)
        {
            account.RegisterFailedLogin(Now);
        }

        account.FailedLoginCount.ShouldBe(AuthPolicy.LockoutFailureThreshold - 1);
        account.IsLockedAt(Now).ShouldBeFalse();
    }

    [Fact]
    public void RegisterFailedLogin_AtThreshold_Locks()
    {
        var account = CreateAccount();

        for (var i = 0; i < AuthPolicy.LockoutFailureThreshold; i++)
        {
            account.RegisterFailedLogin(Now);
        }

        account.IsLockedAt(Now).ShouldBeTrue();
        account.IsLockedAt(Now + AuthPolicy.LockoutDuration + TimeSpan.FromSeconds(1)).ShouldBeFalse();
    }

    [Fact]
    public void RegisterFailedLogin_AfterTheWindowCloses_StartsAFreshCount()
    {
        var account = CreateAccount();

        for (var i = 0; i < AuthPolicy.LockoutFailureThreshold - 1; i++)
        {
            account.RegisterFailedLogin(Now);
        }

        account.FailedLoginCount.ShouldBe(AuthPolicy.LockoutFailureThreshold - 1);

        // One more failure, but well outside the rolling window — must not simply add to the old count.
        var muchLater = Now + AuthPolicy.LockoutWindow + TimeSpan.FromMinutes(1);
        account.RegisterFailedLogin(muchLater);

        account.FailedLoginCount.ShouldBe(1);
        account.IsLockedAt(muchLater).ShouldBeFalse();
    }

    [Fact]
    public void RegisterSuccessfulLogin_ClearsLockoutStateAndRecordsTheTimestamp()
    {
        var account = CreateAccount();

        for (var i = 0; i < AuthPolicy.LockoutFailureThreshold; i++)
        {
            account.RegisterFailedLogin(Now);
        }

        account.IsLockedAt(Now).ShouldBeTrue();

        account.RegisterSuccessfulLogin(Now);

        account.FailedLoginCount.ShouldBe(0);
        account.IsLockedAt(Now).ShouldBeFalse();
        account.LastLoginAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void ChangePassword_MovesTheOldHashIntoHistory_AndClearsMustChangePassword()
    {
        var account = CreateAccount();
        var originalHash = account.AllPasswordHashesForReuseCheck().Single();

        account.ChangePassword("new-hash-1");

        account.MustChangePassword.ShouldBeFalse();
        account.PasswordHash.ShouldBe("new-hash-1");
        account.PasswordHistoryHashes.ShouldContain(originalHash);
    }

    [Fact]
    public void ChangePassword_CapsHistoryAtThePolicyLimit()
    {
        var account = CreateAccount();

        for (var i = 0; i < AuthPolicy.PasswordHistoryLimit + 3; i++)
        {
            account.ChangePassword($"hash-{i}");
        }

        account.PasswordHistoryHashes.Count.ShouldBe(AuthPolicy.PasswordHistoryLimit);
    }

    [Fact]
    public void AllPasswordHashesForReuseCheck_IncludesTheCurrentHashFirst()
    {
        var account = CreateAccount();
        account.ChangePassword("second-hash");

        var all = account.AllPasswordHashesForReuseCheck().ToArray();

        all[0].ShouldBe("second-hash");
        all.ShouldContain("bootstrap-hash");
    }

    [Fact]
    public void Create_NormalizesEmailStaffNameAndPhone()
    {
        var result = AdminAccount.Create(
            Guid.CreateVersion7(), "  Ngozi@Example.COM ", "  Ngozi Adeyemi  ", "08012345678", "some-hash");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe("ngozi@example.com");
        result.Value.StaffName.ShouldBe("Ngozi Adeyemi");
        result.Value.Phone.ShouldBe("+2348012345678");
        result.Value.MustChangePassword.ShouldBeTrue();
        result.Value.IsSuperAdmin.ShouldBeFalse();
        result.Value.Status.ShouldBe(AdminAccountStatus.Active);
    }

    [Fact]
    public void Create_RejectsAStaffNameWithOnlyOneWord()
    {
        var result = AdminAccount.Create(Guid.CreateVersion7(), "a@b.com", "Ngozi", "08012345678", "hash");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("admin.staff_name_invalid");
    }

    [Fact]
    public void Create_RejectsAnInvalidPhone()
    {
        var result = AdminAccount.Create(Guid.CreateVersion7(), "a@b.com", "Two Words", "not-a-phone", "hash");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("admin.phone_invalid");
    }

    [Fact]
    public void ChangeOwnDetails_UpdatesStaffNameAndPhone_NeverEmail()
    {
        var account = CreateRegularAccount();
        var originalEmail = account.Email;

        var result = account.ChangeOwnDetails("New Name Here", "08023456789");

        result.IsSuccess.ShouldBeTrue();
        account.StaffName.ShouldBe("New Name Here");
        account.Phone.ShouldBe("+2348023456789");
        account.Email.ShouldBe(originalEmail);
    }

    [Fact]
    public void UpdateDetails_ChangesNameEmailAndPhoneTogether()
    {
        var account = CreateRegularAccount();

        var result = account.UpdateDetails("Renamed Person", "new@example.com", "08023456789");

        result.IsSuccess.ShouldBeTrue();
        account.StaffName.ShouldBe("Renamed Person");
        account.Email.ShouldBe("new@example.com");
        account.Phone.ShouldBe("+2348023456789");
    }

    [Fact]
    public void SetSuperAdmin_FlipsTheFlagUnconditionally()
    {
        var account = CreateRegularAccount();

        account.SetSuperAdmin(true);

        account.IsSuperAdmin.ShouldBeTrue();
    }

    [Theory]
    [InlineData(AdminAccountStatus.Active, AdminAccountStatus.Suspended, true)]
    [InlineData(AdminAccountStatus.Suspended, AdminAccountStatus.Active, true)]
    [InlineData(AdminAccountStatus.Active, AdminAccountStatus.Deactivated, true)]
    [InlineData(AdminAccountStatus.Suspended, AdminAccountStatus.Deactivated, true)]
    [InlineData(AdminAccountStatus.Deactivated, AdminAccountStatus.Active, true)]
    [InlineData(AdminAccountStatus.Deactivated, AdminAccountStatus.Suspended, false)]
    [InlineData(AdminAccountStatus.Active, AdminAccountStatus.Active, false)]
    public void ChangeStatus_AllowsExactlyTheSpec6110Transitions(
        AdminAccountStatus from,
        AdminAccountStatus to,
        bool expectedAllowed)
    {
        var account = CreateRegularAccount();
        SetStatus(account, from);

        var result = account.ChangeStatus(to);

        result.IsSuccess.ShouldBe(expectedAllowed);

        if (expectedAllowed)
        {
            account.Status.ShouldBe(to);
        }
        else
        {
            result.Error.Code.ShouldBe("admin.invalid_status_transition");
            account.Status.ShouldBe(from);
        }
    }

    [Fact]
    public void ForcePasswordReset_SetsMustChangePasswordTrue_UnlikeASelfServiceChange()
    {
        var account = CreateRegularAccount();
        account.ChangePassword("self-service-hash"); // Clears MustChangePassword to false first.
        account.MustChangePassword.ShouldBeFalse();
        var priorHash = account.PasswordHash;

        account.ForcePasswordReset("forced-hash");

        account.MustChangePassword.ShouldBeTrue();
        account.PasswordHash.ShouldBe("forced-hash");
        account.PasswordHistoryHashes.ShouldContain(priorHash);
    }

    private static AdminAccount CreateAccount() =>
        AdminAccount.CreateBootstrapSuperAdmin(
            Guid.CreateVersion7(), "admin@example.com", "Ada Lovelace", "bootstrap-hash").Value;

    private static AdminAccount CreateRegularAccount() =>
        AdminAccount.Create(
            Guid.CreateVersion7(), "regular@example.com", "Ngozi Adeyemi", "08012345678", "some-hash").Value;

    /// <summary>
    /// Drives the account to <paramref name="status"/> via legal transitions from Active, since
    /// <see cref="AdminAccount.ChangeStatus"/> is the only mutator and there is no direct setter.
    /// </summary>
    private static void SetStatus(AdminAccount account, AdminAccountStatus status)
    {
        if (status == AdminAccountStatus.Active)
        {
            return;
        }

        if (status == AdminAccountStatus.Suspended)
        {
            account.ChangeStatus(AdminAccountStatus.Suspended).IsSuccess.ShouldBeTrue();
            return;
        }

        account.ChangeStatus(AdminAccountStatus.Deactivated).IsSuccess.ShouldBeTrue();
    }
}
