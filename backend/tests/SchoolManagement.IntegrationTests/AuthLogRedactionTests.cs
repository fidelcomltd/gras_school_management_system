using System.Net.Http.Json;
using System.Text;
using SchoolManagement.Application.Auth.ChangePassword;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Proves, end to end through a real sign-in and a real password change, that the submitted
/// plaintext never reaches a log line — CLAUDE.md §6 / spec 9.1: "No token, password or pin appears
/// in a URL, a log line, an error page or an analytics payload... Prove it with a test that asserts
/// the redaction, not by reading the config."
/// </summary>
/// <remarks>
/// Redirects <see cref="Console.Out"/> for the duration of the test and inspects what Serilog's
/// console sink actually wrote — the real output a deployed environment's log collector would
/// ingest — rather than trying to intercept Serilog's pipeline construction, which
/// <c>ObservabilitySetup.ConfigureSerilog</c> finishes building before test code ever runs.
/// </remarks>
public sealed class AuthLogRedactionTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task SignInAndChangePassword_NeverWriteThePlaintextPasswordToConsoleOutput()
    {
        RequireDatabase();

        const string distinctivePassword = "xJ7-VeryDistinctivePlaintext-Marker-4q9";
        const string distinctiveNewPassword = "xJ7-AnotherDistinctiveMarker-New-8k2";

        var (_, email) = await AdminAccountSeeder.SeedAsync(
            Fixture, mustChangePassword: false, seedPassword: distinctivePassword);

        var jar = new CookieJar();
        var capture = new StringBuilder();
        var originalOut = Console.Out;

        try
        {
            await using var writer = new StringWriter(capture);
            Console.SetOut(writer);

            using (var csrfRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf"))
            {
                jar.Apply(csrfRequest);
                var csrfResponse = await Client.SendAsync(csrfRequest, TestContext.Current.CancellationToken);
                jar.Capture(csrfResponse);
            }

            using (var signInRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-in")
            {
                Content = JsonContent.Create(new SignInCommand(email, distinctivePassword)),
            })
            {
                jar.ApplyWithCsrf(signInRequest);
                var signInResponse = await Client.SendAsync(signInRequest, TestContext.Current.CancellationToken);
                signInResponse.EnsureSuccessStatusCode();
                jar.Capture(signInResponse);
            }

            using (var passwordRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/password")
            {
                Content = JsonContent.Create(new ChangePasswordCommand(distinctivePassword, distinctiveNewPassword)),
            })
            {
                jar.ApplyWithCsrf(passwordRequest);
                var passwordResponse = await Client.SendAsync(passwordRequest, TestContext.Current.CancellationToken);
                passwordResponse.EnsureSuccessStatusCode();
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var written = capture.ToString();

        // Second-pass review LOW 7: a positive control. Without it, a broken Console.SetOut redirect
        // (never actually intercepting Serilog's console sink) would leave `capture` empty and both
        // ShouldNotContain assertions below would pass VACUOUSLY — proving nothing. Asserting that
        // something recognisable WAS captured is what makes the negative assertions meaningful.
        written.ShouldNotBeEmpty("No console output was captured at all — the Console.SetOut redirect likely missed Serilog's sink, making the assertions below meaningless.");
        // RequestLoggingBehavior logs the request TYPE NAME unconditionally ("Handling SignInCommand"),
        // so this is present on every run regardless of HTTP-level log formatting/levels.
        written.ShouldContain(nameof(SignInCommand));

        written.ShouldNotContain(distinctivePassword);
        written.ShouldNotContain(distinctiveNewPassword);
    }
}
