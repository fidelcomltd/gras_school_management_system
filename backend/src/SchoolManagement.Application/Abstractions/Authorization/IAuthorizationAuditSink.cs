namespace SchoolManagement.Application.Abstractions.Authorization;

/// <summary>
/// Records a rejected privilege check. Spec 9.2: "Every 403 writes an audit event with outcome
/// rejected, so an account probing routes it does not hold is visible."
/// </summary>
/// <remarks>
/// A SEAM. The audit log module itself (spec 6.1.12 — the <c>audit_event</c> table, its retention
/// and its read endpoints) is out of scope for TASK-0002. The registered implementation
/// (<c>LoggingAuthorizationAuditSink</c>) writes a structured log entry rather than a persisted
/// row; replace it with real <c>audit_event</c> persistence when that module lands.
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
