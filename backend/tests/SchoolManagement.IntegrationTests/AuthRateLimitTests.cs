using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Application.Auth.ChangePassword;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Proves <c>sign-in</c> and <c>password</c> actually run under
/// <see cref="RateLimitingOptions.SensitivePolicyName"/> — the acceptance criterion this whole card is
/// checked against: "sign-in AND password are both rate-limited... each with a test that proves
/// rejection." The sensitive policy has been registered since TASK-0002 with no test ever asserting a
/// 429 against it before this card (<c>HealthRateLimitTests</c> covers only the health policy).
/// </summary>
/// <remarks>
/// Same technique as <c>HealthRateLimitTests</c>: each test builds its OWN host via
/// <c>WithWebHostBuilder</c>, narrowing <see cref="RateLimitingOptions.SensitivePermitLimit"/> to
/// something small enough to trip on purpose, layered on the shared fixture's already-migrated
/// database — no new container, no new migration run.
/// </remarks>
public sealed class AuthRateLimitTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task SignIn_IsRejectedOnceTheSensitivePolicyLimitIsExceeded()
    {
        RequireDatabase();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);

        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.SensitivePermitLimit)}"] = "2",
                [$"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.SensitiveWindowSeconds)}"] = "60",
            })));

        using var client = factory.CreateClient();
        var jar = new CookieJar();

        await GetCsrfAsync(client, jar);

        var first = await SignInAsync(client, jar, email);
        var second = await SignInAsync(client, jar, email);
        var third = await SignInAsync(client, jar, email);

        // The first two consume the whole budget (2) regardless of whether the credentials were
        // right; what matters is that neither was rejected BY THE LIMITER, and the third was.
        first.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);

        third.StatusCode.ShouldBe(
            HttpStatusCode.TooManyRequests,
            "The third sign-in should have exceeded SensitivePermitLimit=2 and been rejected by the " +
            "sensitive rate-limit policy applied in AuthEndpoints.MapSignIn via " +
            "RequireRateLimiting(RateLimitingOptions.SensitivePolicyName).");
    }

    [Fact]
    public async Task ChangePassword_IsRejectedOnceTheSensitivePolicyLimitIsExceeded()
    {
        RequireDatabase();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);

        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.SensitivePermitLimit)}"] = "3",
                [$"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.SensitiveWindowSeconds)}"] = "60",
            })));

        using var client = factory.CreateClient();
        var jar = new CookieJar();

        // The rate limiter runs BEFORE authentication in the middleware pipeline (Program.cs), so
        // ResolveRateLimitPartitionKey never sees an authenticated principal for ANY request — every
        // sensitive-policy call from this client, sign-in included, shares one partition keyed by
        // remote IP. Sign-in therefore spends 1 of the 3-request budget the assertions below check.
        await GetCsrfAsync(client, jar);
        var signIn = await SignInAsync(client, jar, email);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK, "Sign-in itself must not already be throttled by the narrowed limit.");

        var first = await ChangePasswordAsync(client, jar);
        var second = await ChangePasswordAsync(client, jar);
        var third = await ChangePasswordAsync(client, jar);

        first.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        third.StatusCode.ShouldBe(
            HttpStatusCode.TooManyRequests,
            "Sign-in (1) plus two /password calls (2, 3) should have exhausted SensitivePermitLimit=3, " +
            "so this third /password call should have been rejected by the sensitive rate-limit policy " +
            "applied in AuthEndpoints.MapChangePassword via " +
            "RequireRateLimiting(RateLimitingOptions.SensitivePolicyName).");
    }

    private static async Task GetCsrfAsync(HttpClient client, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        jar.Apply(request);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
    }

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, CookieJar jar, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-in")
        {
            Content = JsonContent.Create(new SignInCommand(email, AdminAccountSeeder.Password)),
        };

        jar.ApplyWithCsrf(request);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private static async Task<HttpResponseMessage> ChangePasswordAsync(HttpClient client, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/password")
        {
            Content = JsonContent.Create(
                new ChangePasswordCommand(AdminAccountSeeder.Password, "Rate-Limit-Probe-Passw0rd")),
        };

        jar.ApplyWithCsrf(request);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
