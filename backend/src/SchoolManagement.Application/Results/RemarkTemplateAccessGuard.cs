using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Authorization;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Resolves whether a caller's grants satisfy a remark-template privilege (TASK-0086 stage B).
/// </summary>
/// <remarks>
/// WHY THIS RUNS IN THE HANDLER, NOT AS A ROUTE-DECLARATIVE <c>RequirePrivilege(...)</c> CHECK: the
/// declarative mechanism binds ONE fixed privilege string at route-mapping time, but which of the
/// two remark-template privileges applies depends on a request's <c>kind</c> — a query string on
/// GET, a body field on POST, or a stored row's own kind on DELETE — never a route parameter. The
/// data-dependent pattern <c>PupilAccessGuard</c> already established is the right shape here too:
/// <c>RemarkTemplateEndpoints</c> maps every route with <c>RequireAuthenticatedCaller()</c>, and
/// this guard, together with <see cref="ScopeResolution.AnyGrant"/>, IS the enforcement.
/// </remarks>
internal static class RemarkTemplateAccessGuard
{
    /// <summary>The privilege that governs <paramref name="kind"/>'s template list (ruling T).</summary>
    public static string PrivilegeFor(RemarkKind kind) => kind switch
    {
        RemarkKind.ClassTeacher => Privileges.Results.RemarkClassTeacher,
        RemarkKind.HeadTeacher => Privileges.Results.RemarkHeadTeacher,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown remark kind."),
    };

    /// <summary>
    /// Whether <paramref name="grants"/> hold <paramref name="kind"/>'s privilege under ANY grant —
    /// school-wide or arm-scoped alike (ruling T: "The class-teacher list needs
    /// result.remark.classteacher at ANY scope, and arm-scoped counts").
    /// </summary>
    public static bool HasPrivilegeForKind(IReadOnlyCollection<PrivilegeGrant> grants, RemarkKind kind) =>
        PrivilegeDecision.IsAuthorized(grants, PrivilegeFor(kind), new ScopeResolution.AnyGrant());

    /// <summary>
    /// Whether <paramref name="grants"/> hold EITHER remark-template privilege, under any grant —
    /// the DELETE route's pre-lookup gate (delta item 4: "An unknown id is 404 only for a caller
    /// holding the privilege for at least one kind; anyone else gets 403 first").
    /// </summary>
    public static bool HasPrivilegeForAnyKind(IReadOnlyCollection<PrivilegeGrant> grants) =>
        HasPrivilegeForKind(grants, RemarkKind.ClassTeacher) || HasPrivilegeForKind(grants, RemarkKind.HeadTeacher);
}
