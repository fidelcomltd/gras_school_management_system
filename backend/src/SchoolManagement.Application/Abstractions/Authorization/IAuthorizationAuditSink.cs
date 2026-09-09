namespace SchoolManagement.Application.Abstractions.Authorization;

/// <summary>
/// Records a rejected privilege check. Spec 9.2: "Every 403 writes an audit event with outcome
/// rejected, so an account probing routes it does not hold is visible."
/// </summary>
/// <remarks>
/// Persists a real, durable <c>audit_event</c> row (spec 6.1.12) — TASK-0048 replaced the previous
/// log-only implementation (<c>LoggingAuthorizationAuditSink</c>, DELETED) with
/// <c>Infrastructure/Authorization/AuthorizationAuditSink</c>. This call runs from
/// <c>PrivilegeAuthorizationHandler</c>, in authorization middleware BEFORE the MediatR pipeline —
/// there is no ambient unit-of-work transaction to join here at all, so the row is written and
/// committed directly, the same durable way <c>ISystemAuditSink.RecordRejectionAsync</c> is.
/// </remarks>
public interface IAuthorizationAuditSink
{
    /// <summary>Records that a privilege check failed.</summary>
    /// <param name="userId">
    /// The rejected caller's account id, or <see langword="null"/> if the request never carried an
    /// identity (should not normally reach this sink — an unauthenticated caller is challenged with
    /// 401 before a privilege check runs — but accepted defensively).
    /// </param>
    /// <param name="privilege">The canonical privilege code the route required.</param>
    /// <param name="routePath">The request path, for correlating repeated probing.</param>
    /// <param name="cancellationToken">Propagated to any underlying write.</param>
    Task RecordRejectionAsync(
        string? userId,
        string privilege,
        string? routePath,
        CancellationToken cancellationToken);
}
