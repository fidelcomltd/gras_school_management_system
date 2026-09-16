using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SchoolManagement.Api.Configuration;
using SchoolManagement.Api.Observability;
using SchoolManagement.Api.Security;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Configurations;
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

    /// <summary>
    /// Whether a database was obtained. TASK-0065: always true once <see cref="InitializeAsync"/>
    /// returns — when no database is obtainable, that method THROWS instead, failing every test in the
    /// collection loudly rather than leaving this false for callers to skip around. Kept (rather than
    /// removed) as the defensive flag <see cref="IntegrationTestBase"/> already checks.
    /// </summary>
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

    /// <summary>
    /// Why the database is unavailable. TASK-0065: retained for <see cref="IntegrationTestBase"/>'s
    /// defensive skip path, but in practice <see cref="InitializeAsync"/> now throws before this can be
    /// observed as non-null — see its remarks.
    /// </summary>
    public string? SkipReason { get; private set; }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (await DatabaseAvailability.UnavailableReasonAsync().ConfigureAwait(false) is { } reason)
        {
            // TASK-0065: THROW, not skip. A skipped run is invisible — this is the exact defect that
            // let the Testcontainers fallback go unexercised for months (drift 2026-08-27) because a
            // set POSTGRES_TEST_CONNECTION always took priority and nothing ever forced this path to
            // prove itself. Every test in the collection now fails loudly with this actionable message
            // instead of vanishing from the run as "Skipped".
            SkipReason = reason;
            throw new InvalidOperationException(reason);
        }

        if (DatabaseAvailability.ExternalConnectionString is { } external)
        {
            // An externally provided database takes priority: it is how CI supplies a service container
            // and how a developer without a container runtime can still run the suite.
            _connectionString = external;
        }
        else
        {
            // The endpoint was already resolved (and probed) by the UnavailableReasonAsync call above —
            // ResolvedDockerEndpointAsync is memoized, so this reuses that result rather than probing
            // again. Passed explicitly via WithDockerEndpoint rather than left to ambient $env:DOCKER_HOST:
            // TASK-0065 found that variable set to an address that does not work on the reference
            // machine, so trusting it implicitly would silently undo the resolution above.
            var dockerEndpoint = await DatabaseAvailability.ResolvedDockerEndpointAsync.ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "Docker endpoint resolution changed between the availability check and the " +
                    "container build; this should be unreachable because the result is memoized.");

            // Image passed to the constructor: the parameterless overload is obsolete in
            // Testcontainers 4.13 precisely because it hid which image you were about to run.
            _container = new PostgreSqlBuilder(DatabaseAvailability.PostgresImage)
                .WithDockerEndpoint(dockerEndpoint)
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

        // TASK-0005a: `school_profile` is the first entity in the codebase seeded via migration
        // `HasData` — TRUNCATE wipes that row along with everything else, and unlike a normal
        // migration re-run, EF Core never re-applies seed data on its own. Every integration test
        // relies on the singleton row existing (ISchoolProfileRepository's contract is "always
        // exists"), so it is reinserted here rather than in every test that touches settings.
        await ReseedSchoolProfileAsync(context, cancellationToken);

        // TASK-0028 dispatch 3: same reasoning, for spec 4.5's six seeded roles — TRUNCATE wipes them
        // too, and RoleEndpointsTests' system-role 409 tests need the REAL seeded Super Admin row to
        // exist, not only the fixture-built one `CreateSystemRoleDirectlyAsync` still covers.
        await ReseedRolesAsync(context, cancellationToken);

        // TASK-0038: same reasoning, for spec 6.4.2's two seeded sections and nine seeded levels —
        // ClassLevelEndpointsTests' seeded-chain assertions need the REAL migration-seeded rows.
        await ReseedClassLevelsAsync(context, cancellationToken);

        // TASK-0069: same reasoning, for spec 6.2.13's nine seeded grading bands and the
        // gras_default assessment structure — SettingsGradingAssessmentEndpointsTests' fresh-database
        // assertions need the REAL migration-seeded rows, not an empty table.
        await ReseedGradingBandsAsync(context, cancellationToken);
        await ReseedAssessmentComponentsAsync(context, cancellationToken);
    }

    /// <summary>Reinserts the <see cref="SchoolProfile"/> singleton row, matching the migration's seed data exactly.</summary>
    private static Task<int> ReseedSchoolProfileAsync(ApplicationDbContext context, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO school_profile
                (id, school_name, short_name, abbreviation, address, phone, email, motto,
                 head_teacher_name, timezone, identity_version_number, abbreviation_version_number,
                 separator, serial_width, serial_reset, reg_number_version_number)
            VALUES
                ({SchoolProfile.SingletonId}, '', '', {SchoolProfile.SeededAbbreviation}, '', '', '',
                 NULL, '', {SchoolProfile.FixedTimezone}, 0, 0,
                 {SchoolProfile.DefaultSeparator}, {SchoolProfile.DefaultSerialWidth},
                 {SchoolProfile.DefaultSerialReset.ToString()}, 0)
            """,
            cancellationToken);

    /// <summary>
    /// Reinserts spec 4.5's six seeded roles, built from the SAME <see cref="SeededRoles.All"/> the
    /// <c>SeedRoles</c> migration itself is generated from, so this can never drift from what the
    /// migration actually seeds.
    /// </summary>
    private static async Task ReseedRolesAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        foreach (var role in SeededRoles.All)
        {
            var nameKey = role.Name.ToLowerInvariant();
            var joinedPrivileges = string.Join(',', role.Privileges);

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO roles
                    (id, created_at_utc, created_by, description, is_system, modified_at_utc,
                     modified_by, name, name_key, privileges, status, version)
                VALUES
                    ({role.Id}, {SeededRoles.SeedTimestamp}, NULL, {role.Description}, {role.IsSystem},
                     NULL, NULL, {role.Name}, {nameKey}, {joinedPrivileges}, 'Active', {role.Version})
                """,
                cancellationToken);
        }
    }

    /// <summary>
    /// Reinserts spec 6.4.2's two seeded sections and nine seeded levels, built from the SAME
    /// <see cref="SeededClassLevels.Sections"/>/<see cref="SeededClassLevels.Levels"/> the
    /// <c>SeedClassLevels</c> migration itself is generated from, so this can never drift from what
    /// the migration actually seeds.
    /// </summary>
    private static async Task ReseedClassLevelsAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        foreach (var section in SeededClassLevels.Sections)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO sections
                    (id, created_at_utc, created_by, modified_at_utc, modified_by, name, name_key, version)
                VALUES
                    ({section.Id}, {SeededClassLevels.SeedTimestamp}, NULL, NULL, NULL,
                     {section.Name}, {section.Name.ToLowerInvariant()}, {section.Version})
                """,
                cancellationToken);
        }

        // Reverse chain order (graduating level first) — next_level_id is a self-referencing FK, and
        // an earlier row must already exist before a later row can point at it. Same reasoning as
        // ClassLevelConfiguration's own seed.
        foreach (var level in SeededClassLevels.Levels.Reverse())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO class_levels
                    (id, created_at_utc, created_by, modified_at_utc, modified_by, name, name_key,
                     section_id, progression_order, next_level_id, status, version)
                VALUES
                    ({level.Id}, {SeededClassLevels.SeedTimestamp}, NULL, NULL, NULL, {level.Name},
                     {level.Name.ToLowerInvariant()}, {level.SectionId}, {level.ProgressionOrder},
                     {level.NextLevelId}, 'Active', {level.Version})
                """,
                cancellationToken);
        }
    }

    /// <summary>
    /// Reinserts spec 6.2.13's nine seeded grading bands, using the SAME fixed ids
    /// <see cref="GradingBandConfiguration.SeededIds"/> the migration itself seeds with — reusing the
    /// migration's own row ids, not freshly generated ones and not some other re-derivation, is what
    /// makes this prove the MIGRATION's rows rather than merely proving a seed constant equals itself.
    /// </summary>
    private static async Task ReseedGradingBandsAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var ids = GradingBandConfiguration.SeededIds;

        for (var index = 0; index < GradingScaleSeed.SeededBands.Count; index++)
        {
            var band = GradingScaleSeed.SeededBands[index];

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO grading_band (id, lower_bound, upper_bound, grade_letter, remark, display_order)
                VALUES
                    ({ids[index]}, {band.LowerBound}, {band.UpperBound}, {band.GradeLetter},
                     {band.Remark}, {index + 1})
                """,
                cancellationToken);
        }
    }

    /// <summary>
    /// Reinserts spec 6.2.13's three seeded assessment components (the <c>gras_default</c> profile),
    /// using the SAME fixed ids <see cref="AssessmentComponentConfiguration.SeededIds"/> the migration
    /// itself seeds with — see <see cref="ReseedGradingBandsAsync"/>'s remarks for why.
    /// </summary>
    private static async Task ReseedAssessmentComponentsAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var ids = AssessmentComponentConfiguration.SeededIds;

        for (var index = 0; index < AssessmentStructureSeed.GrasDefaultComponents.Count; index++)
        {
            var component = AssessmentStructureSeed.GrasDefaultComponents[index];

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO assessment_component (id, name, short_label, max_mark, is_examination, display_order)
                VALUES
                    ({ids[index]}, {component.Name}, {component.ShortLabel}, {component.MaxMark},
                     {component.IsExamination}, {index + 1})
                """,
                cancellationToken);
        }
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
