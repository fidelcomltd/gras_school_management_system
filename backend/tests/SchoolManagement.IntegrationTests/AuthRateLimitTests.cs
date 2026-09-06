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

        // Deliberately the WRONG password: a successful sign-in sets the __Host-Session cookie, the
        // jar would replay it on the next call, and TASK-0027's ordering fix means that request would
        // then authenticate and partition by ACCOUNT rather than by IP — a real, correct behaviour
        // change, but not what THIS test is proving. Staying anonymous throughout keeps every call on
        // the same IP-keyed bucket, which is what "the sensitive policy rejects sign-in" is about.
        const string wrongPassword = "Definitely-Not-The-Right-Password-9";
        var first = await SignInAsync(client, jar, email, wrongPassword);
        var second = await SignInAsync(client, jar, email, wrongPassword);
        var third = await SignInAsync(client, jar, email, wrongPassword);

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

        // TASK-0027 fixed Program.cs to run UseAuthentication() before UseRateLimiter(), so
        // ResolveRateLimitPartitionKey now sees the authenticated principal on every request AFTER
        // sign-in. Sign-in itself is still anonymous at the point the limiter runs and so is
        // IP-partitioned — a SEPARATE bucket from the three /password calls below, which are
        // partitioned by the signed-in account's id. Sign-in therefore spends none of the
        // SensitivePermitLimit=3 budget the assertions below check; a FOURTH /password call is what
        // exceeds it, not the third (see AuthRateLimitTests.ChangePassword_PartitionsBySeparateAuthenticatedCaller_NotOnlyByRemoteIp
        // for the test that would actually fail if the ordering fix regressed).
        await GetCsrfAsync(client, jar);
        var signIn = await SignInAsync(client, jar, email);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK, "Sign-in itself must not already be throttled by the narrowed limit.");

        var first = await ChangePasswordAsync(client, jar);
        var second = await ChangePasswordAsync(client, jar);
        var third = await ChangePasswordAsync(client, jar);
        var fourth = await ChangePasswordAsync(client, jar);

        first.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        third.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        fourth.StatusCode.ShouldBe(
            HttpStatusCode.TooManyRequests,
            "Three /password calls (1, 2, 3) should have exhausted this account's own " +
            "SensitivePermitLimit=3 budget, so this fourth call should have been rejected by the " +
            "sensitive rate-limit policy applied in AuthEndpoints.MapChangePassword via " +
            "RequireRateLimiting(RateLimitingOptions.SensitivePolicyName).");
    }

    [Fact]
    public async Task ChangePassword_PartitionsBySeparateAuthenticatedCaller_NotOnlyByRemoteIp()
    {
        RequireDatabase();

        // This is the criterion the card names explicitly: "a 429 test that would still pass under IP
        // partitioning does not satisfy this criterion." Two DIFFERENT accounts, called from the SAME
        // client (and therefore the same remote IP as the test server sees it), must each get their
        // own budget. Under the pre-fix ordering (partition always keyed by IP because
        // ResolveRateLimitPartitionKey never saw an authenticated principal), BOTH sign-ins would share
        // one IP bucket and account B's very first /password call would already be rejected once
        // account A had exhausted it — the assertion on `firstB` below is what would fail.
        var (_, emailA) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var (_, emailB) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);

        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.SensitivePermitLimit)}"] = "2",
                [$"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.SensitiveWindowSeconds)}"] = "60",
            })));

        using var clientA = factory.CreateClient();
        var jarA = new CookieJar();
        await GetCsrfAsync(clientA, jarA);

        var signInA = await SignInAsync(clientA, jarA, emailA);
        signInA.StatusCode.ShouldBe(HttpStatusCode.OK);

        var firstA = await ChangePasswordAsync(clientA, jarA);
        var secondA = await ChangePasswordAsync(clientA, jarA);
        var thirdA = await ChangePasswordAsync(clientA, jarA);

        firstA.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        secondA.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        thirdA.StatusCode.ShouldBe(
            HttpStatusCode.TooManyRequests,
            "Account A's own SensitivePermitLimit=2 budget (2 calls) should be exhausted by its third /password call.");

        using var clientB = factory.CreateClient();
        var jarB = new CookieJar();
        await GetCsrfAsync(clientB, jarB);

        var signInB = await SignInAsync(clientB, jarB, emailB);
        signInB.StatusCode.ShouldBe(HttpStatusCode.OK);

        var firstB = await ChangePasswordAsync(clientB, jarB);

        firstB.StatusCode.ShouldNotBe(
            HttpStatusCode.TooManyRequests,
            "Account B has never called /password before and must have its OWN budget, independent " +
            "of account A's — this is the assertion that would fail if the limiter were still " +
            "partitioning by remote IP instead of by authenticated caller.");
    }

    private static async Task GetCsrfAsync(HttpClient client, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        jar.Apply(request);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
    }

    private static async Task<HttpResponseMessage> SignInAsync(
        HttpClient client, CookieJar jar, string email, string? password = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-in")
        {
            Content = JsonContent.Create(new SignInCommand(email, password ?? AdminAccountSeeder.Password)),
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
