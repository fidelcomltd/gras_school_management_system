using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Persistence;
using SchoolManagement.Application.Abstractions.Secrets;
using SchoolManagement.Application.Reference.SampleRecords;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Interceptors;
using SchoolManagement.Infrastructure.Persistence.Repositories;
using SchoolManagement.Infrastructure.Secrets;

namespace SchoolManagement.Infrastructure;

/// <summary>
/// Registers the infrastructure layer: EF Core, repositories, interceptors, secret provider.
/// </summary>
public static class InfrastructureDependencyInjection
{
    /// <summary>Health-check tag marking checks that gate readiness. Used by <c>/health/ready</c>.</summary>
    public const string ReadinessTag = "ready";

    /// <summary>
    /// Adds persistence and infrastructure services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration, used to bind options.</param>
    /// <param name="validateOnStart">
    /// Whether to require valid database configuration at host start. Pass <c>false</c> only when the
    /// process exists solely to emit the OpenAPI document and will never open a connection — see
    /// <c>HostMode</c> in the Api project.
    /// </param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// Environment-sensitive refusals (for example sensitive data logging enabled in a deployed
    /// environment) are enforced once by <c>StartupEnvironmentGuard</c> in the Api project, at host
    /// start. They are deliberately NOT duplicated here: two copies of a security rule drift apart.
    /// </remarks>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool validateOnStart = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var databaseOptions = services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName));

        if (validateOnStart)
        {
            // Fails at BOOT, not on the first database call. A service that starts and then cannot
            // reach its database looks healthy to an orchestrator until traffic arrives.
            databaseOptions.ValidateOnStart();
        }

        services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();

        // Scoped: it depends on ICurrentUser, which is per-request.
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, builder) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            builder.UseNpgsql(options.ConnectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: options.MaxRetryCount,
                    maxRetryDelay: TimeSpan.FromSeconds(options.MaxRetryDelaySeconds),
                    // null = the provider's built-in list of transient PostgreSQL error codes. Do not
                    // extend it with codes that are not genuinely transient: retrying a deterministic
                    // failure such as a constraint violation just multiplies the work before failing.
                    errorCodesToAdd: null);

                npgsql.CommandTimeout(options.CommandTimeoutSeconds);

                // Must match DesignTimeDbContextFactory, or EF Core sees an empty history table and
                // tries to re-create every table.
                npgsql.MigrationsHistoryTable(ApplicationDbContextDefaults.MigrationsHistoryTable);
            });

            builder.UseSnakeCaseNamingConvention();

            builder.AddInterceptors(serviceProvider.GetRequiredService<AuditingInterceptor>());

            builder.EnableSensitiveDataLogging(options.EnableSensitiveDataLogging);
            builder.EnableDetailedErrors(options.EnableDetailedErrors);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // REFERENCE SCAFFOLD — remove with the SampleRecord slice.
        services.AddScoped<ISampleRecordRepository, SampleRecordRepository>();

        services.AddScoped<ISecretProvider, ConfigurationSecretProvider>();

        // TASK-0002 authorization seams. See the remarks on each type: role/assignment persistence
        // and the audit log module do not exist yet, so these are safe (deny-by-default / logging)
        // stand-ins that a later module replaces.
        services.AddScoped<IEffectivePrivilegeProvider, NullEffectivePrivilegeProvider>();
        services.AddScoped<IAuthorizationAuditSink, LoggingAuthorizationAuditSink>();
        services.AddScoped<IPupilArmOfRecordLookup, NotYetImplementedPupilArmOfRecordLookup>();
        services.AddScoped<IResultSetArmLookup, NotYetImplementedResultSetArmLookup>();

        // Tagged "ready", so /health/ready fails when the database is unreachable while
        // /health/live keeps reporting the process itself as alive. An orchestrator then stops
        // routing traffic here instead of restarting a container that is working fine.
        services
            .AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                name: "database",
                tags: [ReadinessTag]);

        return services;
    }
}
