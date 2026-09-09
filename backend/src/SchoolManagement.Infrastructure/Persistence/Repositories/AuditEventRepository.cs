using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Domain.Audit;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAuditEventRepository"/>, over the AMBIENT
/// <see cref="ApplicationDbContext"/> — the request-scoped instance that the unit-of-work
/// behaviour commits or rolls back. See <see cref="IAuditEventRepository"/>'s own remarks for why
/// there is no update/delete method to implement.
/// </summary>
internal sealed class AuditEventRepository(ApplicationDbContext context) : IAuditEventRepository
{
    /// <inheritdoc />
    public Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        context.AuditEvents.Add(auditEvent);
        return Task.CompletedTask;
    }
}
