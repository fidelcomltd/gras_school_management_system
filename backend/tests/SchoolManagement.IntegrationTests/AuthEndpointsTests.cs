using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SchoolManagement.Api.Endpoints;
using SchoolManagement.Application.Auth;
using SchoolManagement.Application.Auth.ChangePassword;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Domain.Auth;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0003's six <c>/api/v1/auth/*</c> endpoints against the approved contract
/// delta: the CSRF double-submit handshake, sign-in's generic-credentials rule, the three session
/// cookie/CSRF cookie behaviours, and the must-change-password gate (approved delta §2a).
/// </summary>
public sealed class AuthEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string SignOutUrl = "/api/v1/auth/sign-out";
    private const string MeUrl = "/api/v1/auth/me";
    private const string RefreshUrl = "/api/v1/auth/refresh";
    private const string PasswordUrl = "/api/v1/auth/password";

    [Fact]
    public async Task GetCsrf_SetsTheCookieAndReturnsTheSameValueInTheBody()
    {
        RequireDatabase();

        var jar = new CookieJar();
        var response = await GetAsync(CsrfUrl, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await ReadAsync<CsrfTokenResponse>(response);
        jar.CsrfToken.ShouldNotBeNullOrWhiteSpace();
        body.CsrfToken.ShouldBe(jar.CsrfToken);
    }

    [Fact]
    public async Task SignIn_WithoutACsrfToken_Returns403CsrfMissing()
    {
        RequireDatabase();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);

        using var request = new HttpRequestMessage(HttpMethod.Post, SignInUrl)
        {
            Content = JsonContent.Create(new SignInCommand(email, AdminAccountSeeder.Password)),
        };

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("csrf.missing");
    }

    [Fact]
    public async Task SignIn_WithCorrectCredentials_SetsCookiesAndReturnsTheSession()
    {
        RequireDatabase();

        var (accountId, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();

        await GetAsync(CsrfUrl, jar);

        var response = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        jar.HasSession.ShouldBeTrue("A successful sign-in must set the __Host-Session cookie.");

        var session = await ReadAsync<AuthSessionResponse>(response);
        session.AccountId.ShouldBe(accountId.ToString());
        session.Email.ShouldBe(email);
        session.MustChangePassword.ShouldBeFalse();
        session.IsSuperAdmin.ShouldBeTrue();
        session.EffectivePrivileges.ShouldNotBeEmpty(
            "A super-admin's effective privileges are resolved via the flag-bypass path (TASK-0003 §1).");
    }

    [Fact]
    public async Task SignIn_WithAnUnknownEmail_AndAWrongPassword_ReturnTheIdenticalGenericBody()
    {
        RequireDatabase();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);

        var unknownResponse = await PostAsync(
            SignInUrl, jar, new SignInCommand("no-such-account@example.com", "whatever-password-1"));
        var wrongPasswordResponse = await PostAsync(
            SignInUrl, jar, new SignInCommand(email, "definitely-the-wrong-password-1"));

        unknownResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        wrongPasswordResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var unknownBody = await unknownResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var wrongBody = await wrongPasswordResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var unknownDoc = JsonDocument.Parse(unknownBody);
        using var wrongDoc = JsonDocument.Parse(wrongBody);

        unknownDoc.RootElement.GetProperty("errorCode").GetString().ShouldBe("auth.invalid_credentials");
        wrongDoc.RootElement.GetProperty("errorCode").GetString().ShouldBe("auth.invalid_credentials");
        unknownDoc.RootElement.GetProperty("detail").GetString()
            .ShouldBe(wrongDoc.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Me_WhileAnonymous_Returns401AuthenticationRequired()
    {
        RequireDatabase();

        var response = await Client.GetAsync(new Uri(MeUrl, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("authentication.required");
    }

    [Fact]
    public async Task Me_AfterSignIn_ReturnsTheSameAccount()
    {
        RequireDatabase();

        var (accountId, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        var meResponse = await GetAsync(MeUrl, jar);

        meResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await ReadAsync<AuthSessionResponse>(meResponse);
        me.AccountId.ShouldBe(accountId.ToString());
    }

    [Fact]
    public async Task Refresh_ExtendsTheIdleWindow()
    {
        RequireDatabase();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        var original = await ReadAsync<AuthSessionResponse>(signIn);

        var refreshResponse = await PostAsync(RefreshUrl, jar);

        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var refreshed = await ReadAsync<AuthSessionResponse>(refreshResponse);
        refreshed.SessionExpiresAt.ShouldBeGreaterThanOrEqualTo(original.SessionExpiresAt);
    }

    [Fact]
    public async Task GetCsrf_AfterSignIn_DoesNotBreakASubsequentMutation()
    {
        RequireDatabase();

        // Second-pass review HIGH 1: GetCsrfToken's own description says "call once on app load" —
        // the frontend calls it on every reload, including while an authenticated session is already
        // live. If /csrf unconditionally rebinds the cookie to the ANONYMOUS subject, an authenticated
        // caller's next mutation (refresh, password, sign-out) 403s until the session dies. Every
        // OTHER test in this file only ever calls /csrf before signing in — this is the one that
        // exercises the reload-while-signed-in path.
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));

        // Simulates a page reload while already signed in.
        await GetAsync(CsrfUrl, jar);

        var refreshResponse = await PostAsync(RefreshUrl, jar);
        refreshResponse.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "GET /auth/csrf must rebind the CSRF cookie to the CURRENT session when one is live, not " +
            "unconditionally to the anonymous subject.");

        var signOutResponse = await PostAsync(SignOutUrl, jar);
        signOutResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SignOut_ThenMe_Returns401_AndARepeatSignOutStillReturns204()
    {
        RequireDatabase();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));

        var firstSignOut = await PostAsync(SignOutUrl, jar);
        firstSignOut.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var meAfterSignOut = await GetAsync(MeUrl, jar);
        meAfterSignOut.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // A repeat sign-out (already-dead session) still succeeds — approved delta §2. The first
        // sign-out cleared the CSRF cookie too (AuthCookies.ClearCsrf), so a real client would need a
        // fresh token before it could even attempt this — fetch one, exactly as the frontend would.
        await GetAsync(CsrfUrl, jar);
        var secondSignOut = await PostAsync(SignOutUrl, jar);
        secondSignOut.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SignOut_AfterTheIdleTimeoutElapses_StillReturns204()
    {
        RequireDatabase();

        // Second-pass review MEDIUM 4: the session cookie lives 8h while the idle deadline is only 30
        // min, so a sign-out issued in that gap arrives unauthenticated (session expired server-side)
        // but still carrying a CSRF cookie bound to that now-dead session, not to "anon". Without the
        // tolerant path this returns 403 csrf.invalid instead of the 204 approved delta §2 promises
        // "either way... rather than getting a 401 loop".
        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);

        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<TimeProvider>(fakeTime)));
        using var client = factory.CreateClient();
        var jar = new CookieJar();

        await GetAsync(client, CsrfUrl, jar);
        await PostAsync(client, SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));

        fakeTime.Advance(AuthPolicy.IdleTimeout + TimeSpan.FromMinutes(1));

        var signOutResponse = await PostAsync(client, SignOutUrl, jar);
        signOutResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MustChangePasswordGate_BlocksRefreshButNotMeOrPassword()
    {
        RequireDatabase();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: true);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = await ReadAsync<AuthSessionResponse>(signIn);
        session.MustChangePassword.ShouldBeTrue();

        var refreshResponse = await PostAsync(RefreshUrl, jar);
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using (var document = await ReadJsonAsync(refreshResponse))
        {
            document.RootElement.GetProperty("errorCode").GetString().ShouldBe("auth.password_change_required");
        }

        // me stays reachable — approved delta §2a's fix.
        var meResponse = await GetAsync(MeUrl, jar);
        meResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // password stays reachable, and clears the flag.
        var changeResponse = await PostAsync(
            PasswordUrl,
            jar,
            new ChangePasswordCommand(AdminAccountSeeder.Password, "A-Brand-New-Passw0rd"));

        changeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var changed = await ReadAsync<AuthSessionResponse>(changeResponse);
        changed.MustChangePassword.ShouldBeFalse();

        // Now that the flag is clear, refresh works again.
        var refreshAfterChange = await PostAsync(RefreshUrl, jar);
        refreshAfterChange.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_RevokesOtherSessionsButNotTheCurrentOne()
    {
        RequireDatabase();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);

        // Session A: the one that will perform the change.
        var jarA = new CookieJar();
        await GetAsync(CsrfUrl, jarA);
        await PostAsync(SignInUrl, jarA, new SignInCommand(email, AdminAccountSeeder.Password));

        // Session B: a second, independent sign-in for the same account.
        var jarB = new CookieJar();
        await GetAsync(CsrfUrl, jarB);
        await PostAsync(SignInUrl, jarB, new SignInCommand(email, AdminAccountSeeder.Password));

        var changeResponse = await PostAsync(
            PasswordUrl, jarA, new ChangePasswordCommand(AdminAccountSeeder.Password, "Another-New-Passw0rd-2"));
        changeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Session A (performed the change) still works.
        var meA = await GetAsync(MeUrl, jarA);
        meA.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Session B was revoked by the change.
        var meB = await GetAsync(MeUrl, jarB);
        meB.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var document = await ReadJsonAsync(meB);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("authentication.session_revoked");
    }

    [Fact]
    public async Task ChangePassword_WithTheWrongCurrentPassword_Returns401()
    {
        RequireDatabase();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));

        var response = await PostAsync(
            PasswordUrl, jar, new ChangePasswordCommand("totally-wrong-current-password", "Some-New-Passw0rd-3"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("auth.current_password_incorrect");
    }

    [Fact]
    public async Task ChangePassword_ReusingTheCurrentPassword_Returns422FieldKeyedToNewPassword()
    {
        RequireDatabase();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));

        var response = await PostAsync(
            PasswordUrl, jar, new ChangePasswordCommand(AdminAccountSeeder.Password, AdminAccountSeeder.Password));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("request.validation_failed");
        document.RootElement.GetProperty("errors").TryGetProperty("NewPassword", out _).ShouldBeTrue();
    }

    private Task<HttpResponseMessage> GetAsync(string url, CookieJar jar) => GetAsync(Client, url, jar);

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    /// <summary>POSTs with no request body — <c>sign-out</c> and <c>refresh</c>.</summary>
    private Task<HttpResponseMessage> PostAsync(string url, CookieJar jar) => PostAsync(Client, url, jar);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload) =>
        PostAsync(Client, url, jar, payload);

    private static async Task<HttpResponseMessage> PostAsync<T>(HttpClient client, string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };

        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
