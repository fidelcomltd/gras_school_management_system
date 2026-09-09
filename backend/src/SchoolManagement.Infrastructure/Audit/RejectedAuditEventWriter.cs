using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolManagement.Domain.Audit;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Audit;

/// <summary>
/// Writes ONE <see cref="AuditEvent"/> on its own connection and commits it immediately —
/// independent of whatever ambient unit-of-work transaction is (or is not) in progress.
/// </summary>
/// <remarks>
/// <para>
/// THIS IS THE SUBSTANCE OF TASK-0048. <c>UnitOfWork.ExecuteAtomicallyAsync</c> rolls back the
/// ambient transaction whenever a command's <c>Result</c> is a failure, which is precisely when a
/// rejection event is recorded — so joining that transaction would silently discard exactly the
/// rows this mechanism exists to keep (root <c>CLAUDE.md</c> §4.1 decision, 2026-09-09).
/// </para>
/// <para>
/// A FRESH <see cref="ApplicationDbContext"/> INSTANCE, not the ambient one, built from the SAME
/// <see cref="DatabaseOptions"/> (so retry policy, command timeout and the migrations history table
/// name can never drift from the runtime registration in <c>InfrastructureDependencyInjection</c>).
/// A single <c>SaveChangesAsync</c> call with no explicit transaction is EF Core's normal,
/// automatically-retried path under <c>EnableRetryOnFailure</c> — see
/// <see cref="Persistence.UnitOfWork"/>'s own remarks for why an EXPLICIT transaction needs the
/// execution-strategy wrapper this call deliberately avoids needing, by not opening one.
/// </para>
/// <para>
/// The <c>AuditingInterceptor</c> is NOT attached to this context: it depends on the AMBIENT,
/// request-scoped <c>ICurrentUser</c>, and this write must remain independent of that scope. It is
/// also unnecessary — <see cref="AuditEvent"/> is neither <c>IAuditableEntity</c> nor
/// <c>ISoftDeletable</c>, so the interceptor would do nothing for it regardless.
/// </para>
/// </remarks>
internal sealed class RejectedAuditEventWriter(IOptions<DatabaseOptions> databaseOptions)
{
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var options = databaseOptions.Value;

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        optionsBuilder.UseNpgsql(options.ConnectionString, npgsql =>
        {
            npgsql.EnableRetryOnFailure(
                maxRetryCount: options.MaxRetryCount,
                maxRetryDelay: TimeSpan.FromSeconds(options.MaxRetryDelaySeconds),
                errorCodesToAdd: null);

            npgsql.CommandTimeout(options.CommandTimeoutSeconds);
            npgsql.MigrationsHistoryTable(ApplicationDbContextDefaults.MigrationsHistoryTable);
        });

        optionsBuilder.UseSnakeCaseNamingConvention();

        await using var context = new ApplicationDbContext(optionsBuilder.Options);

        context.AuditEvents.Add(auditEvent);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
