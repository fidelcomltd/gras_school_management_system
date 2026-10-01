using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Auth.AdminAccounts;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Infrastructure.Audit;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// TASK-0058, ruled fail-open 2026-09-19: a refusal is still a 403 when its rejection audit row cannot be written. The
/// real <see cref="RejectedAuditEventWriter"/> is pointed at a port nothing listens on, so the write fails the way a
/// database outage would, on its own connection, while the request's own database stays up. One test per path: the
/// route-level privilege check and a handler's escalation rule. Each also proves the lost row was logged (EventId 3100).
/// </summary>
public sealed class AuditWriteFailureTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    [Fact]
    public async Task RouteLevelRefusal_WhenTheAuditWriteFails_StillReturns403()
    {
        RequireDatabase();
        var logs = new CapturedErrors();
        await using var factory = WithFailingRejectionWriter(logs);
        using var client = factory.CreateClient();
        var (_, jar) = await SignInRegularAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/roles");
        jar.Apply(request);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        logs.Errors.ShouldContain(entry => entry.EventId == 3100 && entry.Message.Contains("action role.view", StringComparison.Ordinal), logs.Describe());
    }

    [Fact]
    public async Task HandlerLevelRefusal_WhenTheAuditWriteFails_StillReturns403()
    {
        RequireDatabase();
        var logs = new CapturedErrors();
        await using var factory = WithFailingRejectionWriter(logs);
        using var client = factory.CreateClient();
        var ((id, email, phone), jar) = await SignInRegularAsync(client);

        // Escalation rule 4: a non-Super-Admin granting itself Super Admin.
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/admins/{id}")
        {
            Content = JsonContent.Create(new UpdateAdminAccountCommand(id, "Self", email, phone, true)),
        };
        jar.ApplyWithCsrf(request);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        logs.Errors.ShouldContain(entry => entry.EventId == 3100 && entry.Message.Contains($"actor {id}", StringComparison.Ordinal), logs.Describe());
    }

    // No retries: a refused connection is transient to EF, and three backed-off retries only slow the test.
    private WebApplicationFactory<Program> WithFailingRejectionWriter(CapturedErrors logs) =>
        Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<RejectedAuditEventWriter>();
            services.AddScoped(_ => new RejectedAuditEventWriter(Options.Create(new DatabaseOptions
            {
                ConnectionString = "Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=2",
                MaxRetryCount = 0,
            })));
            // Serilog owns the logger factory, so the two sinks' loggers are replaced directly.
            services.AddSingleton<ILogger<SystemAuditSink>>(new CapturingLogger<SystemAuditSink>(logs));
            services.AddSingleton<ILogger<AuthorizationAuditSink>>(new CapturingLogger<AuthorizationAuditSink>(logs));
        }));

    private async Task<((Guid Id, string Email, string Phone) Account, CookieJar Jar)> SignInRegularAsync(HttpClient client)
    {
        var account = await AdminAccountSeeder.SeedRegularAsync(Fixture, mustChangePassword: false);
        var jar = new CookieJar();
        using (var csrfRequest = new HttpRequestMessage(HttpMethod.Get, CsrfUrl))
        {
            jar.Apply(csrfRequest);
            using var csrf = await client.SendAsync(csrfRequest, TestContext.Current.CancellationToken);
            jar.Capture(csrf);
        }

        using var signInRequest = new HttpRequestMessage(HttpMethod.Post, SignInUrl)
        {
            Content = JsonContent.Create(new SignInCommand(account.Email, AdminAccountSeeder.Password)),
        };
        jar.ApplyWithCsrf(signInRequest);
        using var signIn = await client.SendAsync(signInRequest, TestContext.Current.CancellationToken);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        jar.Capture(signIn);
        return (account, jar);
    }

    private sealed record LogEntry(int EventId, string Message);

    private sealed class CapturedErrors
    {
        private readonly ConcurrentQueue<LogEntry> _errors = new();

        public IReadOnlyCollection<LogEntry> Errors => _errors;

        public void Add(LogEntry entry) => _errors.Enqueue(entry);

        public string Describe() => "captured: " + string.Join(" | ", _errors.Select(entry => $"{entry.EventId}: {entry.Message}"));
    }

    private sealed class CapturingLogger<T>(CapturedErrors errors) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                errors.Add(new LogEntry(eventId.Id, formatter(state, exception)));
            }
        }
    }
}
