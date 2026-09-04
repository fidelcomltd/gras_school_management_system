using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using SchoolManagement.Api.Configuration;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Proves <c>/health/ready</c> actually runs under <see cref="RateLimitingOptions.HealthPolicyName"/>,
/// and that <c>/health/live</c> stays unthrottled even when that policy is tight.
/// </summary>
/// <remarks>
/// <para>
/// AUDIT.md S1 / TASK-0015: before this test existed, <c>.RequireRateLimiting(RateLimitingOptions
/// .HealthPolicyName)</c> in <c>HealthEndpoints.MapHealthEndpoints</c> was the one new production
/// behaviour in that card with nothing asserting it — deleting the line would have left the whole
/// suite green. This closes that gap.
/// </para>
/// <para>
/// Each test builds its OWN client via <c>WithWebHostBuilder</c>, layering a
/// narrowed <c>HealthPermitLimit</c> on top of the shared fixture's already-booted database
/// connection — a new host, but no new container and no new migration run. That is deliberate:
/// the shared fixture (<c>ApiTestFixture.cs</c>) raises <c>HealthPermitLimit</c> to 10000 so every
/// OTHER test's health checks are never throttled, so proving a rejection against the shared client
/// would mean actually driving 10000+ requests — and therefore 10000+ real database round trips —
/// against the shared Neon instance. A permit limit of 2 needs three requests instead.
/// </para>
/// </remarks>
public sealed class HealthRateLimitTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Ready_IsRejectedOnceTheHealthPolicyLimitIsExceeded()
    {
        RequireDatabase();

        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.HealthPermitLimit)}"] =
                    "2",
                [$"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.HealthWindowSeconds)}"] =
                    "60",
            })));

        using var client = factory.CreateClient();
        var readyUri = new Uri("/health/ready", UriKind.Relative);

        // Consume the whole budget (2), then a third request within the same window must be rejected.
        var first = await client.GetAsync(readyUri, TestContext.Current.CancellationToken);
        var second = await client.GetAsync(readyUri, TestContext.Current.CancellationToken);
        var third = await client.GetAsync(readyUri, TestContext.Current.CancellationToken);

        // The first two are not asserted as 200: whether the database check itself reports Healthy is
        // not what this test is about, and asserting it here would make this test fail for the wrong
        // reason. What matters is that neither was rejected BY THE LIMITER.
        first.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);

        third.StatusCode.ShouldBe(
            HttpStatusCode.TooManyRequests,
            "The third request should have exceeded HealthPermitLimit=2 and been rejected by the " +
            "health rate-limit policy applied in HealthEndpoints.MapHealthEndpoints via " +
            "RequireRateLimiting(RateLimitingOptions.HealthPolicyName). If this fails, check whether " +
            "that call was removed or the policy stopped applying to /health/ready.");
    }

    [Fact]
    public async Task Live_StaysUnthrottled_EvenUnderAHealthPolicyTightEnoughToRejectReady()
    {
        RequireDatabase();

        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.HealthPermitLimit)}"] =
                    "1",
                [$"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.HealthWindowSeconds)}"] =
                    "60",
            })));

        using var client = factory.CreateClient();
        var liveUri = new Uri("/health/live", UriKind.Relative);

        // /health/live is mapped with no .RequireRateLimiting at all (HealthEndpoints.cs), so it must
        // keep answering 200 well past a limit that would already have rejected /health/ready.
        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync(liveUri, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }
}
