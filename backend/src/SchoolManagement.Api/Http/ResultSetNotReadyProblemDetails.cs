using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Results;

namespace SchoolManagement.Api.Http;

/// <summary>
/// The DOCUMENTED shape of <c>POST /api/v1/result-sets/{resultSetId}/submit</c>'s <c>422</c> response
/// (TASK-0088 stage B) — the contract's first typed problem-details extension.
/// </summary>
/// <remarks>
/// <para>
/// PURELY FOR THE OPENAPI DOCUMENT. The route declares <c>.Produces&lt;ResultSetNotReadyProblemDetails&gt;
/// (422, "application/problem+json")</c> so the generated schema names <see cref="Readiness"/> as a real
/// property — a type-less <c>.Produces(422)</c> silently drops <c>content</c> from the document
/// (TASK-0049's trap). The RUNTIME response is still produced the ordinary way, by
/// <see cref="ResultExtensions.Match{TValue}"/>'s generic <c>TypedResults.Problem</c> path: when the
/// handler's error is a <see cref="ResultSetNotReadyError"/>, <c>ResultExtensions</c> adds
/// <c>readiness</c> to that response's <c>Extensions</c>, which serialises to the same flat JSON member
/// this type's declared property does — <c>errorCode</c> and <c>traceId</c> already work this way for
/// EVERY problem response (see <c>ProblemDetailsSchemaTransformer</c>), just patched onto the shared
/// generic <see cref="ProblemDetails"/> schema instead of a dedicated one, because none of them needed a
/// structured payload before this.
/// </para>
/// <para>
/// This type is never constructed at runtime — it exists only so the OpenAPI schema generator has
/// something to introspect.
/// </para>
/// </remarks>
public sealed class ResultSetNotReadyProblemDetails : ProblemDetails
{
    /// <summary>Always <see cref="ResultSetNotReadyError.ErrorCode"/> on this response.</summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>Correlation id for this specific response occurrence.</summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// The same body <c>GET /arms/{armId}/readiness</c> returns for this set (AC B6) — what is still
    /// missing, so the screen needs no second call.
    /// </summary>
    public ResultSetReadinessDto Readiness { get; set; } = null!;
}
