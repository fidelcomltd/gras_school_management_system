using SchoolManagement.Domain.Auth;

namespace SchoolManagement.UnitTests.Domain.Auth;

/// <summary>Tests spec 6.1.11's session timeout rules on <see cref="AdminSession"/>.</summary>
public sealed class AdminSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_SetsIdleAndAbsoluteDeadlinesFromPolicy()
    {
        var session = AdminSession.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Now);

        session.IdleExpiresAtUtc.ShouldBe(Now + AuthPolicy.IdleTimeout);
        session.AbsoluteExpiresAtUtc.ShouldBe(Now + AuthPolicy.AbsoluteTimeout);
        session.IsRevoked.ShouldBeFalse();
    }

    [Fact]
    public void IsExpiredAt_IsFalseWhileWithinBothDeadlines()
    {
        var session = AdminSession.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Now);

        session.IsExpiredAt(Now + TimeSpan.FromMinutes(5)).ShouldBeFalse();
    }

    [Fact]
    public void IsExpiredAt_IsTrueOncePastTheIdleDeadline()
    {
        var session = AdminSession.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Now);

        session.IsExpiredAt(Now + AuthPolicy.IdleTimeout + TimeSpan.FromSeconds(1)).ShouldBeTrue();
    }

    [Fact]
    public void IsExpiredAt_IsTrueOncePastTheAbsoluteDeadline_EvenIfIdleWasExtended()
    {
        var session = AdminSession.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Now);

        var justBeforeAbsolute = Now + AuthPolicy.AbsoluteTimeout - TimeSpan.FromMinutes(1);
        session.ExtendIdle(justBeforeAbsolute);

        session.IsExpiredAt(Now + AuthPolicy.AbsoluteTimeout + TimeSpan.FromSeconds(1)).ShouldBeTrue();
    }

    [Fact]
    public void ExtendIdle_NeverPushesPastTheAbsoluteDeadline()
    {
        var session = AdminSession.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Now);

        var justBeforeAbsolute = Now + AuthPolicy.AbsoluteTimeout - TimeSpan.FromMinutes(1);
        session.ExtendIdle(justBeforeAbsolute);

        session.IdleExpiresAtUtc.ShouldBe(session.AbsoluteExpiresAtUtc);
    }

    [Fact]
    public void ExtendIdle_WellBeforeTheAbsoluteDeadline_AddsTheFullIdleWindow()
    {
        var session = AdminSession.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Now);

        var later = Now + TimeSpan.FromMinutes(10);
        session.ExtendIdle(later);

        session.IdleExpiresAtUtc.ShouldBe(later + AuthPolicy.IdleTimeout);
    }

    [Fact]
    public void Revoke_IsIdempotent_KeepsTheFirstReasonAndTimestamp()
    {
        var session = AdminSession.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Now);

        session.Revoke(Now, AdminSessionRevocationReasons.SignOut);
        session.Revoke(Now + TimeSpan.FromMinutes(1), AdminSessionRevocationReasons.PasswordChanged);

        session.RevokedReason.ShouldBe(AdminSessionRevocationReasons.SignOut);
        session.RevokedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void RotateToken_ChangesOnlyTheHash_NotTheSessionIdentityOrDeadlines()
    {
        var session = AdminSession.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "original-hash", Now);
        var id = session.Id;
        var absoluteBefore = session.AbsoluteExpiresAtUtc;

        session.RotateToken("new-hash");

        session.TokenHash.ShouldBe("new-hash");
        session.Id.ShouldBe(id);
        session.AbsoluteExpiresAtUtc.ShouldBe(absoluteBefore);
    }
}
