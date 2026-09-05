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

    private static AdminAccount CreateAccount() =>
        AdminAccount.CreateBootstrapSuperAdmin(
            Guid.CreateVersion7(), "admin@example.com", "Ada Lovelace", "bootstrap-hash").Value;
}
