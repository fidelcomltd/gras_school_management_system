using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Domain.Audit;
using SchoolManagement.Infrastructure.Audit;

namespace SchoolManagement.Infrastructure.Authorization;

/// <summary>
/// Real, persisted <see cref="IAuthorizationAuditSink"/> (spec 6.1.12, spec 9.2). TASK-0048
/// replaces <c>LoggingAuthorizationAuditSink</c> (DELETED, no longer registered anywhere).
/// </summary>
/// <remarks>
/// Called from <c>PrivilegeAuthorizationHandler</c>, in authorization middleware BEFORE the
/// MediatR pipeline — there is no ambient unit-of-work transaction here at all, so every call
/// commits immediately through the same <see cref="RejectedAuditEventWriter"/>
/// <c>ISystemAuditSink.RecordRejectionAsync</c> uses, for one consistent "a rejection is durable
/// the same way everywhere" story. Still logs (<see cref="AuthorizationLog.PrivilegeCheckRejected"/>,
/// unchanged from the deleted seam) so the rejection is visible in observability immediately,
/// not only on the next audit-log read.
/// </remarks>
internal sealed class AuthorizationAuditSink(
    AuditEventFactory factory,
    RejectedAuditEventWriter rejectedWriter,
    ILogger<AuthorizationAuditSink> logger)
    : IAuthorizationAuditSink
{
    /// <summary>
    /// Spec 6.1.12's <c>entity_type</c> is required, but a rejected privilege check has no natural
    /// entity — the route probed may not even name one. This fixed value satisfies the column
    /// without inventing a fake per-route type.
    /// </summary>
    private const string EntityType = "privilege_check";

    /// <inheritdoc />
    public async Task RecordRejectionAsync(
        string? userId,
        string privilege,
        string? routePath,
        CancellationToken cancellationToken)
    {
        AuthorizationLog.PrivilegeCheckRejected(logger, userId ?? "(none)", privilege, routePath ?? "(unknown)");

        var metadata = routePath is null
            ? null
            : new Dictionary<string, object?>(StringComparer.Ordinal) { ["routePath"] = routePath };

        var auditEvent = await factory
            .BuildAsync(
                AuditOutcome.Rejected,
                privilege,
                EntityType,
                entityId: null,
                metadata,
                userId,
                reason: null,
                cancellationToken)
            .ConfigureAwait(false);

        await rejectedWriter.WriteAsync(auditEvent, cancellationToken).ConfigureAwait(false);
    }
}
