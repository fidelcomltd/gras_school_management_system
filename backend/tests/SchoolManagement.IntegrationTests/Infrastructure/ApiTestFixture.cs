using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Observability;
using SchoolManagement.Api.Security;
using SchoolManagement.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SchoolManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the REAL application against a REAL PostgreSQL, once per test collection.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is stubbed. The pipeline, the middleware order, the authorisation fallback policy, EF Core,
/// migrations and the error contract are all exercised exactly as they run in production. That is the
/// point: a test that replaces any of those is testing a different application.
/// </para>
/// <para>
/// Shared across a whole collection rather than created per test because starting a container and
/// applying migrations costs seconds. Tests therefore must not depend on each other's data — see
/// <see cref="ResetDatabaseAsync"/>.
/// </para>
/// </remarks>
public sealed class ApiTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    /// <summary>Whether a database was obtained. When false, every test in the collection skips.</summary>
    public bool IsDatabaseAvailable { get; private set; }

    /// <summary>
    /// The connection string this fixture migrated, valid once <see cref="IsDatabaseAvailable"/> is
    /// true. TASK-0019: lets a test build a SECOND, purpose-built minimal host (see
    /// <c>Idempotency/IdempotencyTestHost.cs</c>) against the SAME already-migrated database, without
    /// adding a production route to <c>Program</c> just to have something to test against.
    /// </summary>
    public string ConnectionString => _connectionString
        ?? throw new InvalidOperationException(
            $"{nameof(ConnectionString)} is unavailable — {nameof(IsDatabaseAvailable)} is false.");

    /// <summary>Why the database is unavailable, for the skip message.</summary>
    public string? SkipReason { get; private set; }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (DatabaseAvailability.UnavailableReason is { } reason)
        {
            SkipReason = reason;
            IsDatabaseAvailable = false;
            return;
        }

        if (DatabaseAvailability.ExternalConnectionString is { } external)
        {
            // An externally provided database takes priority: it is how CI supplies a service container
            // and how a developer without a container runtime can still run the suite.
            _connectionString = external;
        }
        else
        {
            // Image passed to the constructor: the parameterless overload is obsolete in
            // Testcontainers 4.13 precisely because it hid which image you were about to run.
            _container = new PostgreSqlBuilder(DatabaseAvailability.PostgresImage)
                .WithDatabase("schoolmanagement_tests")
                // Credentials for a throwaway container that exists for the length of this test run and
                // is then destroyed. Not a secret: it is reachable only from this machine, holds only
                // test data, and nothing outside this process is ever configured with it.
                .WithUsername("test_user")
                .WithPassword("test_password")
                .WithCleanUp(true)
                .Build();

            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        IsDatabaseAvailable = true;

        // Migrations are applied HERE, not by the application at startup. Production applies them as a
        // deliberate deployment step (see docs/adr/0006), so the app has no auto-migrate code path for a
        // test to lean on — the test does what the deployment does.
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Development, so the placeholder authentication scheme is permitted and StartupEnvironmentGuard
        // does not refuse to start. The tests then verify that protected endpoints return 401 under that
        // scheme, which is the deny-by-default guarantee actually being exercised.
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.ConnectionString)}"] =
                    _connectionString,

                // Retries off: a transient-failure retry inside a test turns a genuine failure into a
                // slow, confusing one.
                [$"{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.MaxRetryCount)}"] = "0",

                // TASK-0003: CookieSession is now the real, implemented mechanism. It authenticates
                // nobody without a valid session cookie — same anonymous-by-default behaviour every
                // other test in this shared fixture already relies on — so switching the default here
                // does not change any of them; only auth-specific test classes present a real cookie.
                [$"{ApiAuthenticationOptions.SectionName}:{nameof(ApiAuthenticationOptions.Mode)}"] =
                    AuthenticationModes.CookieSession,

                // No telemetry export: there is no collector, and failing exports would flood the output.
                [$"{ObservabilityOptions.SectionName}:{nameof(ObservabilityOptions.OtlpEndpoint)}"] = "",

                // High enough that a test suite hitting the same endpoint repeatedly is not throttled
                // under the DEFAULT policy.
                [$"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.PermitLimit)}"] = "10000",

                // TASK-0003: sign-in and password now run under the SENSITIVE policy (its first real
                // users), and the auth integration test suite calls both repeatedly, from one shared
                // host, from what the limiter sees as one IP — same reasoning as PermitLimit above.
                // AuthRateLimitTests is the one place this policy is actually verified: like
                // HealthRateLimitTests, it builds its own client via WithWebHostBuilder with this
                // narrowed to something small enough to trip on purpose.
                [$"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.SensitivePermitLimit)}"] =
                    "10000",

                // /health/ready runs under its own policy (RateLimitingOptions.HealthPolicyName), not
                // the default one raised above, so it needs its own headroom for the same reason — this
                // is the shared fixture's limit, kept high so every OTHER test's health checks are never
                // throttled. HealthRateLimitTests is the one place that policy is actually verified: it
                // builds its own client via WithWebHostBuilder with this narrowed to something small
                // enough to trip on purpose.
                [$"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.HealthPermitLimit)}"] =
                    "10000",
            });
        });
    }

    /// <summary>
    /// Truncates all application tables so each test starts from a known state.
    /// </summary>
    /// <remarks>
    /// TRUNCATE rather than dropping and re-migrating: it is fast enough to call per test, and it keeps
    /// the schema (and therefore every constraint) in place. The migrations history table is excluded, or
    /// the next test would try to migrate an already-migrated database.
    /// </remarks>
    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tableNames = context.Model
            .GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .Where(tableName => !string.IsNullOrWhiteSpace(tableName))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (tableNames.Length == 0)
        {
            return;
        }

        var quoted = string.Join(", ", tableNames.Select(tableName => $"\"{tableName}\""));

        // Table names come from the EF Core model, never from user input or a test parameter, so there is
        // no injection surface. Built into a local rather than interpolated at the call site so EF1002
        // (which flags interpolated SQL arguments) is satisfied by construction instead of suppressed.
        // RESTART IDENTITY resets sequences; CASCADE handles foreign keys between the truncated tables.
        var truncateSql = string.Concat("TRUNCATE TABLE ", quoted, " RESTART IDENTITY CASCADE;");

        await context.Database.ExecuteSqlRawAsync(truncateSql, cancellationToken);
    }

    /// <summary>Creates a scope for resolving application services inside a test.</summary>
    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();
}

/// <summary>
/// Shares one <see cref="ApiTestFixture"/> across every integration test class.
/// </summary>
/// <remarks>
/// One container and one migration run for the whole suite. Tests within the collection run
/// sequentially, which is what makes <see cref="ApiTestFixture.ResetDatabaseAsync"/> safe.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class ApiTestCollectionDefinition : ICollectionFixture<ApiTestFixture>
{
    /// <summary>The collection name test classes reference.</summary>
    public const string Name = "Api integration tests";
}
