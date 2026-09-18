using SchoolManagement.Domain.Audit;

namespace SchoolManagement.UnitTests.Domain.Audit;

/// <summary>
/// Spec 6.1.12 / spec 14 §9.3's PII rule: <c>source_ip</c> truncated to /24 (IPv4) or /48 (IPv6),
/// <c>user_agent</c> truncated at 300 characters, before either reaches storage.
/// </summary>
public sealed class AuditFieldTruncationTests
{
    [Fact]
    public void TruncateSourceIp_WithNull_ReturnsNull() =>
        AuditFieldTruncation.TruncateSourceIp(null).ShouldBeNull();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-ip-address")]
    [InlineData("999.999.999.999")]
    public void TruncateSourceIp_WithAnUnparseableValue_ReturnsNull(string rawValue) =>
        AuditFieldTruncation.TruncateSourceIp(rawValue).ShouldBeNull();

    [Fact]
    public void TruncateSourceIp_WithIpv4_TruncatesToSlash24() =>
        AuditFieldTruncation.TruncateSourceIp("203.0.113.42").ShouldBe("203.0.113.0");

    [Fact]
    public void TruncateSourceIp_WithIpv4AlreadyOnTheNetworkBoundary_IsUnchanged() =>
        AuditFieldTruncation.TruncateSourceIp("203.0.113.0").ShouldBe("203.0.113.0");

    [Fact]
    public void TruncateSourceIp_WithIpv6_TruncatesToSlash48() =>
        AuditFieldTruncation.TruncateSourceIp("2001:db8:85a3:1234:5678:8a2e:370:7334").ShouldBe("2001:db8:85a3::");

    [Fact]
    public void TruncateSourceIp_WithIpv4MappedIpv6_IsHandledAsTheEmbeddedIpv4Address() =>
        AuditFieldTruncation.TruncateSourceIp("::ffff:203.0.113.42").ShouldBe("203.0.113.0");

    [Fact]
    public void TruncateUserAgent_WithNull_ReturnsNull() =>
        AuditFieldTruncation.TruncateUserAgent(null).ShouldBeNull();

    [Fact]
    public void TruncateUserAgent_ShorterThanTheLimit_IsUnchanged() =>
        AuditFieldTruncation.TruncateUserAgent("Mozilla/5.0").ShouldBe("Mozilla/5.0");

    [Fact]
    public void TruncateUserAgent_ExactlyAtTheLimit_IsUnchanged()
    {
        var exact = new string('a', AuditFieldTruncation.UserAgentMaxLength);

        AuditFieldTruncation.TruncateUserAgent(exact).ShouldBe(exact);
    }

    [Fact]
    public void TruncateUserAgent_LongerThanTheLimit_TruncatesWithoutThrowing()
    {
        var overlong = new string('a', AuditFieldTruncation.UserAgentMaxLength + 500);

        var truncated = AuditFieldTruncation.TruncateUserAgent(overlong);

        truncated.ShouldNotBeNull();
        truncated.Length.ShouldBe(AuditFieldTruncation.UserAgentMaxLength);
    }
}
