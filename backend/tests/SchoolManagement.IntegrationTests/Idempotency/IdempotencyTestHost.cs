using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolManagement.Api.Idempotency;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Idempotency;
using SchoolManagement.Domain.Reference;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Interceptors;
using SchoolManagement.Infrastructure.Persistence.Repositories;

namespace SchoolManagement.IntegrationTests.Idempotency;

/// <summary>
/// A minimal, purpose-built ASP.NET Core test host proving the idempotency substrate end to end
/// against a REAL PostgreSQL, WITHOUT the production route TASK-0019's card explicitly forbids ("do
/// NOT add a production route to have something to test against — register one on the test host").
/// </summary>
/// <remarks>
/// <para>
/// WHY A SECOND HOST RATHER THAN <c>ApiTestFixture</c>/<c>Program</c> PLUS AN ADDED ROUTE: TASK-0019
/// ships NO route that declares <c>Idempotency-Key</c> — that is TASK-0027's job, and the approved
/// delta requires <c>generate-openapi.ps1</c> to produce a byte-identical document. Adding even a
/// disabled-by-default endpoint module to the Api assembly risks exactly the drift the card's own
/// instruction is guarding against. This host is a SEPARATE, small application — real
/// <see cref="ApplicationDbContext"/>, real <c>IdempotencyStore</c>, real
/// <see cref="RequireIdempotencyKeyExtensions.RequireIdempotencyKey{TBuilder}"/> filter — pointed at
/// the SAME already-migrated database <see cref="Infrastructure.ApiTestFixture"/> set up, so nothing
/// under test here is a stand-in: only the ONE test-only route and its handler are.
/// </para>
/// <para>
/// The "side effect" the concurrency/replay tests count is a real INSERT into
/// <see cref="SampleRecord"/> — the project's own reference scaffold, already migrated into the same
/// database, chosen so the assertion is "how many rows exist" rather than a mock call count.
/// </para>
/// </remarks>
internal sealed class IdempotencyTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private IdempotencyTestHost(WebApplication app, HttpClient client)
    {
        _app = app;
        Client = client;
    }

    /// <summary>An <see cref="HttpClient"/> wired directly to this host's in-memory <c>TestServer</c>.</summary>
    public HttpClient Client { get; }

    /// <summary>The header a caller's identity is read from — see <see cref="TestCallerCurrentUser"/>.</summary>
    public const string CallerHeaderName = "X-Test-Caller";

    /// <summary>The route the probe command is posted to.</summary>
    public const string ProbeRoute = "/probe";

    /// <summary>Starts a host bound to <paramref name="connectionString"/>.</summary>
    public static async Task<IdempotencyTestHost> StartAsync(string connectionString)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(json =>
            json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, TestCallerCurrentUser>();

        builder.Services.AddScoped<AuditingInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable(ApplicationDbContextDefaults.MigrationsHistoryTable))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(serviceProvider.GetRequiredService<AuditingInterceptor>());
        });

        // The exact three services RequireIdempotencyKey()'s filter resolves, wired to the REAL
        // implementations — nothing here is a fake.
        builder.Services.AddScoped<IIdempotencyStore, IdempotencyStore>();

        // A no-op stub, not the real TASK-0048 SystemAuditSink: this host is deliberately minimal
        // (see the class remarks — only the probe route/handler is a stand-in for anything) and
        // does not register IAdminAccountRepository or IOptions<DatabaseOptions>, which the real
        // implementation needs. Audit persistence is not what this host proves.
        builder.Services.AddScoped<ISystemAuditSink, NoOpSystemAuditSink>();
        builder.Services.AddScoped<IdempotencyPurgeJob>();
        builder.Services.Configure<IdempotencyOptions>(_ => { });

        var app = builder.Build();

        app.MapPost(ProbeRoute, async (
                ProbeCommand command,
                ApplicationDbContext context,
                CancellationToken cancellationToken) =>
            {
                var creation = SampleRecord.Create(Guid.CreateVersion7(), command.Value, note: null);

                if (creation.IsFailure)
                {
                    return Results.Problem(creation.Error.Description, statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                context.Add(creation.Value);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return Results.Created($"{ProbeRoute}/{creation.Value.Id}", new ProbeResponse(
                    creation.Value.Id,
                    command.Secret));
            })
            .RequireIdempotencyKey(required: true);

        await app.StartAsync().ConfigureAwait(false);

        var testServer = app.Services.GetRequiredService<IServer>() as TestServer
            ?? throw new InvalidOperationException("Expected the TestServer implementation of IServer.");

        return new IdempotencyTestHost(app, testServer.CreateClient());
    }

    /// <summary>Opens a scope for resolving services directly (used by the purge/redaction tests).</summary>
    public AsyncServiceScope CreateScope() => _app.Services.CreateAsyncScope();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// The test-only "mutating request" posted to <see cref="IdempotencyTestHost.ProbeRoute"/>.
/// Implements <see cref="IBaseCommand"/> directly (not the full <see cref="ICommand{TResponse}"/>/
/// mediator shape) — the filter's fingerprinting only needs the marker interface, and this host
/// deliberately does not wire the mediator, to keep it small and focused on the idempotency
/// mechanism rather than re-proving the pipeline <c>PipelineTests</c> already covers.
/// </summary>
/// <param name="Value">Becomes the inserted <see cref="SampleRecord"/>'s label — the "one side effect".</param>
/// <param name="Secret">
/// Echoed back on <see cref="ProbeResponse.Secret"/>, marked
/// <see cref="RedactFromIdempotencyReplayAttribute"/> there — proves the redaction hook.
/// </param>
internal sealed record ProbeCommand(string Value, string? Secret = null) : IBaseCommand;

/// <summary>The response to a successful <see cref="ProbeCommand"/>.</summary>
/// <param name="Id">The inserted <see cref="SampleRecord"/>'s id.</param>
/// <param name="Secret">
/// The live caller always sees the real value; the STORED replay copy has this redacted to
/// <see langword="null"/> — see <see cref="RedactFromIdempotencyReplayAttribute"/>.
/// </param>
internal sealed record ProbeResponse(Guid Id, [property: RedactFromIdempotencyReplayAttribute] string? Secret);

/// <summary>
/// Reads the caller identity from <see cref="IdempotencyTestHost.CallerHeaderName"/> instead of a
/// real authenticated principal — this host has no cookie-session auth wired at all, and the
/// idempotency mechanism only needs SOME stable per-caller identity, which a real deployment gets
/// from <c>HttpCurrentUser</c> instead.
/// </summary>
internal sealed class TestCallerCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string? UserId => httpContextAccessor.HttpContext?.Request
        .Headers[IdempotencyTestHost.CallerHeaderName]
        .ToString() is { Length: > 0 } value
        ? value
        : null;

    public bool IsAuthenticated => UserId is not null;

    public string? RemoteIpAddress => null;

    public string? UserAgent => null;
}

/// <summary>Discards everything. See the registration site's remarks for why.</summary>
internal sealed class NoOpSystemAuditSink : ISystemAuditSink
{
    public Task RecordAsync(
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        string? actorAdminId,
        CancellationToken cancellationToken,
        string? reason = null) => Task.CompletedTask;

    public Task RecordRejectionAsync(
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        string? actorAdminId,
        CancellationToken cancellationToken,
        string? reason = null) => Task.CompletedTask;
}
