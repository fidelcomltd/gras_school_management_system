using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Abstractions.Authorization;

namespace SchoolManagement.Infrastructure.Authorization;

/// <summary>
/// Default <see cref="IAuthorizationAuditSink"/>: writes a structured log entry.
/// </summary>
/// <remarks>
/// TASK-0002 SEAM. The audit log module (spec 6.1.12 — the persisted, append-only
/// <c>audit_event</c> table) is out of scope for this card. This implementation satisfies "every
/// 403 writes an audit event with outcome rejected" (spec 9.2) with a log line rather than a row,
/// so the rejection is visible in observability today.
/// TODO(TASK-0002): replace with real <c>audit_event</c> persistence, sharing the write's
/// transaction, once the audit log module exists.
/// </remarks>
internal sealed class LoggingAuthorizationAuditSink(ILogger<LoggingAuthorizationAuditSink> logger)
    : IAuthorizationAuditSink
{
    /// <inheritdoc />
    public Task RecordRejectionAsync(
        string? userId,
        string privilege,
        string? routePath,
        CancellationToken cancellationToken)
    {
        AuthorizationLog.PrivilegeCheckRejected(
            logger,
            userId ?? "(none)",
            privilege,
            routePath ?? "(unknown)");

        return Task.CompletedTask;
    }
}
