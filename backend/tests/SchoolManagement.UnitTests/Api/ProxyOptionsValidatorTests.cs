using Microsoft.AspNetCore.Builder;
using SchoolManagement.Api.Configuration;

namespace SchoolManagement.UnitTests.Api;

/// <summary>
/// Tests the reverse-proxy trust configuration. Security-relevant on both sides: forwarding headers
/// honoured from an unnamed proxy let a caller choose its own client address (and with it the rate
/// limit bucket, the portal's per-address pin limits and the audit trail's source_ip), while
/// forwarding NOT applied behind Nginx collapses all three onto loopback.
/// </summary>
public sealed class ProxyOptionsValidatorTests
{
    private static readonly ProxyOptionsValidator Validator = new();

    private static ProxyOptions Enabled(params string[] knownProxies)
    {
        var options = new ProxyOptions { Enabled = true };

        foreach (var proxy in knownProxies)
        {
            options.KnownProxies.Add(proxy);
        }

        return options;
    }

    [Fact]
    public void Validate_AcceptsLoopbackProxies()
    {
        Validator.Validate(null, Enabled("127.0.0.1", "::1")).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AcceptsDisabledForwardingWithoutAnyProxy()
    {
        // The shipped default, and every local run: nothing is in front of Kestrel.
        Validator.Validate(null, new ProxyOptions()).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RejectsEnabledForwardingWithNoProxyNamed()
    {
        var result = Validator.Validate(null, new ProxyOptions { Enabled = true });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("no proxy is named");
    }

    [Fact]
    public void Validate_RejectsTrustAllProxiesCombinedWithAnAllowList()
    {
        var options = Enabled("127.0.0.1");
        options.TrustAllProxies = true;

        var result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("would be");
    }

    [Fact]
    public void Validate_AcceptsTrustAllProxiesAlone()
    {
        // The staging shape: a PaaS edge whose address is not published.
        var options = new ProxyOptions { Enabled = true, TrustAllProxies = true };

        Validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("127.0.0.1/8")]
    public void Validate_RejectsUnparsableProxyAddresses(string proxy)
    {
        var result = Validator.Validate(null, Enabled(proxy));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("not an IP address");
    }

    [Fact]
    public void Validate_RejectsUnparsableNetworks()
    {
        var options = new ProxyOptions { Enabled = true };
        options.KnownNetworks.Add("10.0.0.0");

        var result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("CIDR");
    }

    [Fact]
    public void Validate_RejectsAForwardLimitBelowOne()
    {
        var options = Enabled("127.0.0.1");
        options.ForwardLimit = 0;

        var result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ForwardLimit");
    }

    [Fact]
    public void ApplyTo_ForwardsOnlyForAndProto_AndTrustsExactlyWhatWasNamed()
    {
        var options = Enabled("127.0.0.1");
        options.KnownNetworks.Add("10.0.0.0/8");

        var forwardedHeaders = new ForwardedHeadersOptions();
        options.ApplyTo(forwardedHeaders);

        forwardedHeaders.ForwardedHeaders.ShouldBe(
            Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
            | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto);

        // Exactly the configured entries — the framework's own loopback defaults are cleared, so
        // widening cannot arrive from a framework change.
        forwardedHeaders.KnownProxies.ShouldHaveSingleItem().ToString().ShouldBe("127.0.0.1");
        forwardedHeaders.KnownIPNetworks.ShouldHaveSingleItem().ToString().ShouldBe("10.0.0.0/8");
        forwardedHeaders.ForwardLimit.ShouldBe(1);
    }

    [Fact]
    public void ApplyTo_WithTrustAllProxies_LeavesTheAllowListsEmpty()
    {
        var options = new ProxyOptions { Enabled = true, TrustAllProxies = true };

        var forwardedHeaders = new ForwardedHeadersOptions();
        options.ApplyTo(forwardedHeaders);

        forwardedHeaders.KnownProxies.ShouldBeEmpty();
        forwardedHeaders.KnownIPNetworks.ShouldBeEmpty();
    }
}
