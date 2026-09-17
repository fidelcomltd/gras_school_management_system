using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Audit;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Idempotency;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Reference;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// The application's EF Core context.
/// </summary>
/// <remarks>
/// <para>
/// RULES FOR THIS CLASS:
/// </para>
/// <list type="bullet">
/// <item>No per-entity fluent configuration in <see cref="OnModelCreating"/>. Mapping lives in
/// <c>IEntityTypeConfiguration&lt;T&gt;</c> classes under <c>Persistence/Configurations</c> and is
/// picked up by assembly scanning. A 400-line <c>OnModelCreating</c> is unreviewable and produces
/// constant merge conflicts; one file per entity does not.</item>
/// <item>No public <c>DbSet</c> properties. Application code reaches data through repository
/// abstractions, so a query cannot be written in a layer that has no business issuing one.</item>
/// <item>Not registered as a service anything outside this project can resolve.</item>
/// </list>
/// <para>
/// Tracking behaviour is left at EF Core's default (tracking) rather than being switched to
/// no-tracking globally. Read paths call <c>AsNoTracking</c> explicitly — an audited, visible choice
/// per query — because a global no-tracking default makes writes silently fail to persist, which is
/// a far worse failure mode than a forgotten <c>AsNoTracking</c> on a read. See
/// <c>docs/adr/0006-persistence-conventions.md</c>.
/// </para>
/// </remarks>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    /// <summary>
    /// REFERENCE SCAFFOLD — remove alongside <see cref="SampleRecord"/>.
    /// Internal, not public: only this assembly's repositories may query it.
    /// </summary>
    internal DbSet<SampleRecord> SampleRecords => Set<SampleRecord>();

    /// <summary>TASK-0003. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<AdminAccount> AdminAccounts => Set<AdminAccount>();

    /// <summary>TASK-0003. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<AdminSession> AdminSessions => Set<AdminSession>();

    /// <summary>TASK-0019. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    /// <summary>TASK-0005a. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<SchoolProfile> SchoolProfiles => Set<SchoolProfile>();

    /// <summary>TASK-0005a. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<ConfigVersion> ConfigVersions => Set<ConfigVersion>();

    /// <summary>TASK-0005c. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<RegistrationCounter> RegistrationCounters => Set<RegistrationCounter>();

    /// <summary>TASK-0069. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<GradingBand> GradingBands => Set<GradingBand>();

    /// <summary>TASK-0069. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<AssessmentComponent> AssessmentComponents => Set<AssessmentComponent>();

    /// <summary>TASK-0028 dispatch 2. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<Role> Roles => Set<Role>();

    /// <summary>TASK-0030. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    /// <summary>TASK-0035. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<AcademicSession> AcademicSessions => Set<AcademicSession>();

    /// <summary>TASK-0035. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<Term> Terms => Set<Term>();

    /// <summary>TASK-0038. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<Section> Sections => Set<Section>();

    /// <summary>TASK-0038. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<ClassLevel> ClassLevels => Set<ClassLevel>();

    /// <summary>TASK-0039. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<Arm> Arms => Set<Arm>();

    /// <summary>
    /// TASK-0048. Internal, not public: only <see cref="Repositories.AuditEventRepository"/> and the
    /// audit sinks in <c>Infrastructure/Audit</c> and <c>Infrastructure/Authorization</c> ever touch
    /// this set — it has no update or delete path anywhere (spec 9.4).
    /// </summary>
    internal DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>
    /// TASK-0050. Internal, not public: only this assembly's repositories may query it. Carries the
    /// pending-exclusion model-level query filter (spec 6.5.14) — see
    /// <c>Configurations.PupilConfiguration</c>.
    /// </summary>
    internal DbSet<Pupil> Pupils => Set<Pupil>();

    /// <summary>
    /// TASK-0059. Internal, not public: only this assembly's repositories may query it. Spec 02
    /// §5.2's arm-of-record relationship — no <c>Pupil</c> or <c>Arm</c> column ever substitutes
    /// for a query against this set for the open row.
    /// </summary>
    internal DbSet<Enrolment> Enrolments => Set<Enrolment>();

    /// <summary>
    /// TASK-0062. Internal, not public: only this assembly's repositories may query it. Sections A, I
    /// and J of the admission form (spec 6.5.9) — one row per <see cref="Pupil"/>, created in the same
    /// transaction as the pupil.
    /// </summary>
    internal DbSet<AdmissionRecord> AdmissionRecords => Set<AdmissionRecord>();

    /// <summary>
    /// TASK-0063. Internal, not public: only this assembly's repositories may query it. Every
    /// superseded registration number, appended by a correction and never updated or deleted (spec
    /// 6.5.10) — see <c>Configurations.PupilRegNumberHistoryConfiguration</c>.
    /// </summary>
    internal DbSet<PupilRegNumberHistory> PupilRegNumberHistory => Set<PupilRegNumberHistory>();

    /// <summary>TASK-0070. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<Subject> Subjects => Set<Subject>();

    /// <summary>
    /// TASK-0070. Internal, not public: only this assembly's repositories may query it. NO rows are
    /// seeded — a mapping needs a <c>term_id</c> and no session or term is seeded (see
    /// <c>SubjectConfiguration</c>'s remarks and <c>PrefillSubjectMappingsHandler</c>).
    /// </summary>
    internal DbSet<SubjectMapping> SubjectMappings => Set<SubjectMapping>();

    /// <summary>TASK-0070. Internal, not public: only this assembly's repositories may query it.</summary>
    internal DbSet<SubjectMappingException> SubjectMappingExceptions => Set<SubjectMappingException>();

    /// <summary>
    /// TASK-0076 dispatch A. Internal, not public: only this assembly's repositories may query it.
    /// One row per arm per term (spec 09 §6.7.3).
    /// </summary>
    internal DbSet<ResultSet> ResultSets => Set<ResultSet>();

    /// <summary>
    /// TASK-0076 dispatch A. Internal, not public: only this assembly's repositories may query it.
    /// One row per pupil per subject per term (spec 09 §6.7.3).
    /// </summary>
    internal DbSet<SubjectScore> SubjectScores => Set<SubjectScore>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        ApplySoftDeleteQueryFilters(modelBuilder);
        ApplyConcurrencyTokens(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Shadow property holding each row's optimistic-concurrency token.
    /// </summary>
    /// <remarks>
    /// A SHADOW property, so domain entities carry no persistence-concern field. The value is maintained
    /// by <c>AuditingInterceptor</c>.
    /// </remarks>
    public const string ConcurrencyTokenProperty = "Version";

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Every string column is bounded unless an entity configuration says otherwise. Npgsql maps
        // an unbounded string to `text`, which is fine for PostgreSQL but means nothing stops a
        // multi-megabyte value arriving in a column intended to hold a name.
        configurationBuilder.Properties<string>().HaveMaxLength(DefaultStringMaxLength);

        // DateTimeOffset -> timestamptz, which stores an instant in UTC. The repo-wide rule is that
        // instants are DateTimeOffset in UTC; `timestamp without time zone` is banned because it
        // silently discards the offset and turns an ordering bug into a data bug.
        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");

        base.ConfigureConventions(configurationBuilder);
    }

    /// <summary>Default maximum length applied to every string property.</summary>
    private const int DefaultStringMaxLength = 256;

    /// <summary>
    /// Adds <c>WHERE NOT is_deleted</c> to every <see cref="ISoftDeletable"/> entity.
    /// </summary>
    /// <remarks>
    /// Applied by reflection over the model rather than written per entity, so a new soft-deletable
    /// entity is filtered automatically. Without this, excluding deleted rows would be the
    /// responsibility of every individual query, and the first one to forget silently exposes
    /// deleted data.
    /// <para>
    /// To include deleted rows deliberately, a query must call <c>IgnoreQueryFilters()</c> — which
    /// is greppable, unlike its absence.
    /// </para>
    /// </remarks>
    private static void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var isDeleted = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var notDeleted = Expression.Not(isDeleted);

            modelBuilder
                .Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(notDeleted, parameter));
        }
    }

    /// <summary>
    /// Gives every auditable entity an optimistic-concurrency token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY NOT PostgreSQL's <c>xmin</c>. It is the natural choice — every row already carries the
    /// transaction ID that last wrote it, so it costs no column and no application code. Npgsql's
    /// <c>UseXminAsConcurrencyToken()</c> supported it, but that API no longer exists in the EF Core 10
    /// provider, and mapping <c>xmin</c> by hand makes the migration try to CREATE a column with that
    /// name — which PostgreSQL rejects, because it collides with the system column. Every new entity
    /// would then need its migration edited by hand, and generated migrations must not be hand-tuned.
    /// </para>
    /// <para>
    /// So the token is an application-maintained GUID instead. It costs 16 bytes per row and one
    /// assignment per save, and in exchange it is explicit in the schema, portable off PostgreSQL, and
    /// needs no per-migration surgery. EF Core puts the ORIGINAL value in the UPDATE's WHERE clause, so a
    /// write that lost a race affects zero rows and surfaces as <c>DbUpdateConcurrencyException</c> —
    /// which the global handler turns into a 409 rather than silently overwriting somebody's change.
    /// </para>
    /// <para>
    /// Applied by convention here rather than per entity, so a new aggregate is protected without anyone
    /// remembering to opt in.
    /// </para>
    /// </remarks>
    private static void ApplyConcurrencyTokens(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IAuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            modelBuilder
                .Entity(entityType.ClrType)
                .Property<Guid>(ConcurrencyTokenProperty)
                .IsConcurrencyToken()
                // The application supplies the value; the database must not generate one, or EF would
                // expect a value back and the token would never match.
                .ValueGeneratedNever();
        }
    }
}
