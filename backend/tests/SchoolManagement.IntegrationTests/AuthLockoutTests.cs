using System.Net;
using System.Net.Http.Json;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Spec 6.1.11's lockout rule end to end: "Five failed attempts in fifteen minutes locks the account
/// for fifteen minutes," and the approved contract delta's binding ruling — <c>423</c> fires ONLY when
/// the submitted password is correct.
/// </summary>
public sealed class AuthLockoutTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task FiveWrongAttempts_LockTheAccount_AndTheCorrectPasswordThenReturns423WithLockedUntil()
    {
        RequireDatabase();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetCsrfAsync(jar);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await SignInAsync(jar, email, "wrong-password-attempt");
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

            using var document = await ReadJsonAsync(response);
            document.RootElement.GetProperty("errorCode").GetString().ShouldBe("auth.invalid_credentials");
        }

        // The account is now locked. The CORRECT password is the only thing that reveals it.
        var correctPasswordResponse = await SignInAsync(jar, email, AdminAccountSeeder.Password);

        correctPasswordResponse.StatusCode.ShouldBe((HttpStatusCode)423);
        using var lockedDocument = await ReadJsonAsync(correctPasswordResponse);
        var root = lockedDocument.RootElement;
        root.GetProperty("errorCode").GetString().ShouldBe("auth.account_locked");
        root.TryGetProperty("lockedUntil", out var lockedUntil).ShouldBeTrue();
        lockedUntil.GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ALockedAccount_GivenAWrongPassword_StillGetsTheGenericBody_NotTheLockedStatus()
    {
        RequireDatabase();

        var (_, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        await GetCsrfAsync(jar);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await SignInAsync(jar, email, "wrong-password-attempt");
        }

        // Locked now — but a WRONG password still gets the ordinary generic 401, never 423. This is
        // the timing-oracle-closing rule the approved delta's §2 binds implementation to.
        var response = await SignInAsync(jar, email, "still-the-wrong-password");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("auth.invalid_credentials");
    }

    private async Task GetCsrfAsync(CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        jar.Apply(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
    }

    private async Task<HttpResponseMessage> SignInAsync(CookieJar jar, string email, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-in")
        {
            Content = JsonContent.Create(new SignInCommand(email, password)),
        };

        jar.ApplyWithCsrf(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
