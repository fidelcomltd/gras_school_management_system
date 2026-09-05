using System.Net.Http.Json;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Proves — rather than reads — that CORS, the cookie attribute set and the frontend's expected
/// <c>credentials</c> mode form ONE coherent set end to end (CLAUDE.md §5): "A frontend sending
/// `credentials: 'include'` against a backend not configured for it is a blocker... prove it with an
/// integration test, not by reading."
/// </summary>
/// <remarks>
/// Second-pass review HIGH 3: this test previously INJECTED its own
/// <c>Cors:AllowedOrigins</c>/<c>AllowCredentials</c> configuration via <c>WithWebHostBuilder</c>
/// before asserting — proving the mechanism works under a configuration the repo does not actually
/// ship, while <c>appsettings.json</c> shipped <c>AllowCredentials: false</c> (§5's named blocker,
/// live). This now asserts against the SHARED fixture's client, which loads the real, committed
/// <c>appsettings.json</c> unmodified — the placeholder origin below must match that file exactly.
/// </remarks>
public sealed class AuthCorsAndCookieCoherenceTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    /// <summary>Must match the placeholder entry in the committed <c>appsettings.json</c>'s <c>Cors:AllowedOrigins</c>.</summary>
    private const string FrontendOrigin = "https://app.example.com";

    [Fact]
    public async Task ACredentialedCrossOriginSignIn_GetsMatchingCorsHeaders_AndSetsCoherentCookies()
    {
        RequireDatabase();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();

        // GET /auth/csrf, as a credentialed cross-origin request would arrive.
        using var csrfRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        csrfRequest.Headers.Add("Origin", FrontendOrigin);
        var csrfResponse = await Client.SendAsync(csrfRequest, TestContext.Current.CancellationToken);
        jar.Capture(csrfResponse);

        AssertCoherentCorsHeaders(csrfResponse);

        using var signInRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-in")
        {
            Content = JsonContent.Create(new SignInCommand(email, AdminAccountSeeder.Password)),
        };
        signInRequest.Headers.Add("Origin", FrontendOrigin);
        jar.ApplyWithCsrf(signInRequest);

        var signInResponse = await Client.SendAsync(signInRequest, TestContext.Current.CancellationToken);
        jar.Capture(signInResponse);

        signInResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        AssertCoherentCorsHeaders(signInResponse);

        // The cookie attribute set itself: HttpOnly + Secure + SameSite=Lax on the session cookie,
        // Secure + SameSite=Lax but NOT HttpOnly on the CSRF cookie (axios must be able to read it) —
        // approved contract delta §6, proven against the real Set-Cookie headers rather than read off
        // AuthCookies.cs.
        var setCookies = signInResponse.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.ToArray()
            : [];

        var sessionCookie = setCookies
            .Where(candidate => candidate.StartsWith("__Host-Session=", StringComparison.Ordinal))
            .ShouldHaveSingleItem();
        sessionCookie.ShouldContain("HttpOnly", Case.Insensitive);
        sessionCookie.ShouldContain("Secure", Case.Insensitive);
        sessionCookie.ShouldContain("SameSite=Lax", Case.Insensitive);
        sessionCookie.ShouldContain("Path=/", Case.Insensitive);

        var csrfCookie = setCookies
            .Where(candidate => candidate.StartsWith("__Host-XSRF-TOKEN=", StringComparison.Ordinal))
            .ShouldHaveSingleItem();
        csrfCookie.ShouldNotContain("HttpOnly", Case.Insensitive);
        csrfCookie.ShouldContain("Secure", Case.Insensitive);
        csrfCookie.ShouldContain("SameSite=Lax", Case.Insensitive);
    }

    private static void AssertCoherentCorsHeaders(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOrigin)
            .ShouldBeTrue("Access-Control-Allow-Origin must be present for a credentialed cross-origin request to succeed in a real browser.");
        allowOrigin!.ShouldContain(FrontendOrigin);

        // Credentialed requests specifically require the LITERAL string "true", not merely presence —
        // a browser rejects "Access-Control-Allow-Credentials: false" (or its absence) when the
        // request carried credentials, which is exactly the failure mode CLAUDE.md §5 calls a blocker.
        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var allowCredentials)
            .ShouldBeTrue();
        allowCredentials!.ShouldContain("true");
    }
}
