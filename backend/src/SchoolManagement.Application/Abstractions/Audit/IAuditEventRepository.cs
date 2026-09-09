using SchoolManagement.Domain.Audit;

namespace SchoolManagement.Application.Abstractions.Audit;

/// <summary>
/// Persistence port for <see cref="AuditEvent"/> — ADD ONLY, deliberately.
/// </summary>
/// <remarks>
/// Spec 9.4: "audit_event | Never [hard-deleted] | Nothing. Pruned only by the retention
/// schedule." and spec 6.1.12: "Entries cannot be edited or deleted through any interface, and no
/// endpoint exists that would allow it." This interface has no <c>Update</c>/<c>Delete</c>/
/// <c>Remove</c> method so that guarantee cannot be quietly reopened by a later change — held by
/// <c>AuditEventRepositoryTests.HasNoUpdateOrDeleteMethod</c> (architecture test).
/// <para>
/// No read method either: the read surface (filters, paging, CSV export, <c>audit.view</c>/
/// <c>audit.export</c>) is TASK-0049, out of this card's scope.
/// </para>
/// </remarks>
public interface IAuditEventRepository
{
    /// <summary>
    /// Adds one row to the ambient <c>DbContext</c>'s change tracker. No <c>SaveChangesAsync</c> —
    /// the caller decides how and when this commits (the unit-of-work behaviour for a success
    /// event; a dedicated short-lived connection for a rejected one — see
    /// <c>Infrastructure/Audit/SystemAuditSink</c>).
    /// </summary>
    Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}
