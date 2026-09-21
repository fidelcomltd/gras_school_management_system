using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Abstractions.Admissions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Persistence;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Secrets;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Idempotency;
using SchoolManagement.Application.Reference.SampleRecords;
using SchoolManagement.Application.Settings;
using SchoolManagement.Infrastructure.Audit;
using SchoolManagement.Infrastructure.Auth;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.Infrastructure.Idempotency;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Interceptors;
using SchoolManagement.Infrastructure.Persistence.Repositories;
using SchoolManagement.Infrastructure.Results;
using SchoolManagement.Infrastructure.Secrets;
using SchoolManagement.Infrastructure.Settings;

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

        // Stateless, so a singleton is safe even though GlobalExceptionHandler (its only current
        // consumer) is itself registered as a singleton by AddExceptionHandler.
        services.AddSingleton<IPersistenceErrorTranslator, PersistenceErrorTranslator>();

        // REFERENCE SCAFFOLD — remove with the SampleRecord slice.
        services.AddScoped<ISampleRecordRepository, SampleRecordRepository>();

        services.AddScoped<ISecretProvider, ConfigurationSecretProvider>();

        // TASK-0030: the graduated implementation, resolving real role_assignment rows for every
        // non-super-admin account — replaces SuperAdminFlagEffectivePrivilegeProvider (TASK-0003),
        // DELETED, not left registered behind a flag. See the class remarks.
        services.AddScoped<IEffectivePrivilegeProvider, RoleAssignmentEffectivePrivilegeProvider>();
        services.AddScoped<IAuthorizationAuditSink, AuthorizationAuditSink>();
        // TASK-0059: the real implementation, resolving a pupil's arm from their open enrolment —
        // replaces NotYetImplementedPupilArmOfRecordLookup (DELETED, not left registered behind a
        // flag, same convention TASK-0030 used for SuperAdminFlagEffectivePrivilegeProvider).
        services.AddScoped<IPupilArmOfRecordLookup, PupilArmOfRecordLookup>();
        // TASK-0076 dispatch A: the real implementation, resolving a result set's arm with a direct
        // query against result_set — replaces NotYetImplementedResultSetArmLookup (DELETED, not left
        // registered behind a flag, same convention TASK-0059 used above for
        // NotYetImplementedPupilArmOfRecordLookup).
        services.AddScoped<IResultSetArmLookup, ResultSetArmLookup>();

        // TASK-0003: authentication and session management (spec 6.1.11, spec 9.1). Bound the same
        // way DatabaseOptions is above — the Api project's AddValidatedOptions helper is off-limits
        // here (Infrastructure must not depend on Api; see DependencyDirectionTests.NothingDependsOnApi).
        var argon2Options = services
            .AddOptions<Argon2Options>()
            .Bind(configuration.GetSection(Argon2Options.SectionName));

        if (validateOnStart)
        {
            argon2Options.ValidateOnStart();
        }

        services.AddSingleton<IValidateOptions<Argon2Options>, Argon2OptionsValidator>();

        services.AddScoped<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddScoped<IAdminAccountRepository, AdminAccountRepository>();
        services.AddScoped<IAdminSessionRepository, AdminSessionRepository>();
        services.AddScoped<IAdminSessionAuthenticator, AdminSessionAuthenticator>();

        // TASK-0019: the idempotency substrate. IIdempotencyStore is called once, from the API
        // layer's RequireIdempotencyKey() endpoint filter — never per-endpoint. The hosted service
        // runs the retention purge (§9.9) on a schedule; a test that needs a deterministic run
        // resolves IdempotencyPurgeJob directly instead.
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IdempotencyPurgeJob>();
        services.AddHostedService<IdempotencyPurgeBackgroundService>();

        // TASK-0048: the real, persisted audit_event trail (spec 6.1.12, spec 14 §9.3), replacing
        // both LoggingSystemAuditSink and LoggingAuthorizationAuditSink (DELETED) together — see
        // ISystemAuditSink's own remarks for why a rejection needs its own writer.
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<AuditEventFactory>();
        services.AddScoped<RejectedAuditEventWriter>();
        services.AddScoped<ISystemAuditSink, SystemAuditSink>();

        // TASK-0049: the read surface — filters, cursor paging, CSV export. Deliberately a SEPARATE
        // port from IAuditEventRepository, which stays ADD-ONLY (see its own remarks).
        services.AddScoped<IAuditEventQueryRepository, AuditEventQueryRepository>();

        // TASK-0005a: school identity and the append-only config_version ledger.
        services.AddScoped<ISchoolProfileRepository, SchoolProfileRepository>();
        services.AddScoped<IConfigVersionRepository, ConfigVersionRepository>();

        // TASK-0005c: registration-number counter, read paths only — see the port's own remarks.
        services.AddScoped<IRegistrationCounterRepository, RegistrationCounterRepository>();

        // TASK-0069: grading scale and assessment structure.
        services.AddScoped<IGradingBandRepository, GradingBandRepository>();
        services.AddScoped<IAssessmentComponentRepository, AssessmentComponentRepository>();

        // TASK-0077: result rules.
        services.AddScoped<IResultRulesRepository, ResultRulesRepository>();

        // TASK-0072 stage 1: rating scales.
        services.AddScoped<IRatingScaleRepository, RatingScaleRepository>();

        // TASK-0072 stage 2a: development domains and indicators. IRatingScaleUsageGate is now a real
        // query against development_domain, replacing stage 1's unconditional "not in use" stand-in.
        services.AddScoped<IRatingScaleUsageGate, RatingScaleUsageGate>();
        services.AddScoped<IDevelopmentDomainRepository, DevelopmentDomainRepository>();

        // TASK-0083 stage 2: development ratings. IDevelopmentIndicatorUsageGate is now a real query
        // against development_rating, replacing TASK-0072 stage 2a's unconditional "never rated"
        // stand-in.
        services.AddScoped<IDevelopmentIndicatorUsageGate, DevelopmentIndicatorUsageGate>();
        services.AddScoped<IDevelopmentRatingRepository, DevelopmentRatingRepository>();

        // TASK-0072 stage 3b: traits.
        services.AddScoped<ITraitRepository, TraitRepository>();

        // TASK-0083 stage 1: trait ratings. ITraitUsageGate is now a real query against trait_rating,
        // replacing TASK-0072 stage 3b's unconditional "never rated" stand-in.
        services.AddScoped<ITraitUsageGate, TraitUsageGate>();
        services.AddScoped<ITraitRatingRepository, TraitRatingRepository>();

        // TASK-0086 stage A: attendance and the two remark sheets.
        services.AddScoped<IAttendanceEntryRepository, AttendanceEntryRepository>();
        services.AddScoped<IPupilRemarkRepository, PupilRemarkRepository>();

        // TASK-0086 stage B: remark templates.
        services.AddScoped<IRemarkTemplateRepository, RemarkTemplateRepository>();

        // TASK-0072 stage 3a: one seam every settings handler asks for the whole config_version
        // snapshot input through, replacing the one-repository-per-OTHER-group constructor ripple —
        // see ISettingsSnapshotSource's own remarks.
        services.AddScoped<ISettingsSnapshotSource, SettingsSnapshotSource>();

        // TASK-0076 dispatch A: real queries against subject_score/result_set, replacing the
        // honestly-empty TASK-0069 stand-ins now that the tables exist.
        services.AddScoped<ISubjectScoreSessionLockLookup, SubjectScoreSessionLockLookup>();
        services.AddScoped<IPublishedResultsGate, PublishedResultsGate>();
        services.AddScoped<IResultSetRepository, ResultSetRepository>();

        // TASK-0076 dispatch B: the score-sheet endpoints' own mark persistence.
        services.AddScoped<ISubjectScoreRepository, SubjectScoreRepository>();

        // TASK-0071: the computation engine's own persistence for the three computed tables.
        services.AddScoped<IResultComputationRepository, ResultComputationRepository>();

        // TASK-0028 dispatch 2: role persistence and CRUD.
        services.AddScoped<IRoleRepository, RoleRepository>();

        // TASK-0030: role assignments — the graduated provider above depends on this.
        services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();

        // TASK-0035: academic sessions and terms.
        services.AddScoped<IAcademicSessionRepository, AcademicSessionRepository>();
        services.AddScoped<ITermRepository, TermRepository>();

        // TASK-0038: sections and class levels.
        services.AddScoped<ISectionRepository, SectionRepository>();
        services.AddScoped<IClassLevelRepository, ClassLevelRepository>();

        // TASK-0039: arms.
        services.AddScoped<IArmRepository, ArmRepository>();

        services.AddScoped<IPupilRepository, PupilRepository>();

        // TASK-0059: enrolment — dated membership of a pupil in an arm (spec 02 §5.2).
        services.AddScoped<IEnrolmentRepository, EnrolmentRepository>();

        // TASK-0062: admission_record — sections A, I and J of the admission form (spec 6.5.9).
        services.AddScoped<IAdmissionRecordRepository, AdmissionRecordRepository>();

        // TASK-0063: the permanent registration-number history alias (spec 6.5.10).
        services.AddScoped<IPupilRegNumberHistoryRepository, PupilRegNumberHistoryRepository>();

        // TASK-0070: subjects, level mappings and per-arm exceptions (spec 6.6).
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<ISubjectMappingRepository, SubjectMappingRepository>();
        services.AddScoped<ISubjectMappingExceptionRepository, SubjectMappingExceptionRepository>();

        // TASK-0076 dispatch A: a real query against subject_score, replacing the honestly-empty
        // TASK-0070 stand-in now that the table exists.
        services.AddScoped<ISubjectMappingMarkLookup, SubjectMappingMarkLookup>();

        // TASK-0005b stage A: logo/signature re-encoding. Stateless and thread-safe (each call
        // decodes and encodes its own bitmaps), so singleton rather than per-request.
        services.AddSingleton<ISchoolImageProcessor, SkiaSchoolImageProcessor>();

        // TASK-0005b stage B1: PLACEHOLDER registration. The in-memory fake is registered
        // unconditionally here because nothing consumes ISchoolImageStore yet (upload routes are
        // stage B2); stage D replaces this with an environment-based choice between this fake (tests)
        // and the real CloudinaryImageStore (everywhere else) — "tests never touch the network"
        // (orchestrator design, 2026-09-21). Singleton so uploads persist for the lifetime of the
        // process, matching a real store's behaviour closely enough for a fake.
        services.AddSingleton<ISchoolImageStore, InMemorySchoolImageStore>();

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
