using System.Net;
using SchoolManagement.Api.Portal;
using SchoolManagement.Application.Portal;

namespace SchoolManagement.UnitTests.Application.Portal;

public sealed class PortalRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 12, 14, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("gras/2026/0041", "GRAS20260041")]
    [InlineData(" GRAS-2026-0041 ", "GRAS20260041")]
    [InlineData("gras 2026.0041", "GRAS20260041")]
    public void RegistrationNumbers_AreMatchedWithoutSeparators(string typed, string expected) =>
        PortalValues.NormalizeRegistrationNumber(typed).ShouldBe(expected);

    [Fact]
    public void Block_StartsAtTheLimitInsideTheWindow_AndLiftsAfterTheBlock()
    {
        var failures = Enumerable.Range(0, 5).Select(minute => Now.AddMinutes(-50 + minute)).ToList();

        PortalLookupHandler.IsBlocked(failures, Now, limit: 5, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60)).ShouldBeTrue();
        PortalLookupHandler.IsBlocked(failures, Now.AddMinutes(20), limit: 5, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60)).ShouldBeFalse();
        PortalLookupHandler.IsBlocked(failures.Take(4).ToList(), Now, limit: 5, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60)).ShouldBeFalse();
    }

    [Fact]
    public void Block_NeedsTheFailuresInsideOneWindow()
    {
        var spread = Enumerable.Range(0, 10).Select(index => Now.AddMinutes(-index * 3)).ToList();

        PortalLookupHandler.IsBlocked(spread, Now, limit: 10, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("102.89.34.77", "102.89.34.0/24")]
    [InlineData("::ffff:102.89.34.77", "102.89.34.0/24")]
    [InlineData("2c0f:f5c0:1:2:3:4:5:6", "2c0f:f5c0:1::/48")]
    public void Addresses_AreTruncatedBeforeStorage(string address, string expected) =>
        PortalEndpoints.TruncatedAddress(IPAddress.Parse(address)).ShouldBe(expected);
}
