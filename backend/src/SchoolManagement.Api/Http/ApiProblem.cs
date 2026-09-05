using SchoolManagement.Domain.Common;

namespace SchoolManagement.Api.Http;

/// <summary>
/// The single mapping from a domain <see cref="ErrorType"/> to an HTTP status code and problem type.
/// </summary>
/// <remarks>
/// <para>
/// THIS IS THE ONLY PLACE IN THE CODEBASE THAT DECIDES A STATUS CODE. No handler, endpoint or
/// behaviour names one. That is what stops two endpoints returning different codes for the same kind
/// of failure — the classic API inconsistency, where one endpoint 404s for a missing row and another
/// 400s.
/// </para>
/// <para>
/// <c>ErrorTypeMappingTests</c> enumerates <see cref="ErrorType"/> and fails if any member is
/// unmapped, so adding an error category cannot silently fall through to 500.
/// </para>
/// </remarks>
internal static class ApiProblem
{
    /// <summary>
    /// URN prefix for the RFC 9457 <c>type</c> member.
    /// </summary>
    /// <remarks>
    /// A URN, not an <c>https://</c> URL. RFC 9457 says <c>type</c> SHOULD dereference to
    /// documentation; we do not yet own a documentation host, and inventing
    /// <c>https://example.com/errors/...</c> would be a URI that looks resolvable and is not. A URN is
    /// a valid URI reference that makes no such promise, and it is stable — which is the property
    /// clients actually branch on. Swap this constant when a real docs site exists; the error codes
    /// themselves do not change.
    /// </remarks>
    public const string TypeUrnPrefix = "urn:schoolmanagement:error:";

    /// <summary>Maps an error category to its HTTP status code.</summary>
    /// <param name="errorType">The category to map.</param>
    public static int ToStatusCode(ErrorType errorType) => errorType switch
    {
        // 422, not 400. The root convention requires picking one and being consistent: 400 means the
        // request was malformed (unparseable JSON, bad query-string type), which ASP.NET Core's model
        // binder returns before our code runs. 422 means it parsed fine but failed a business rule.
        // Keeping them distinct lets a client tell "I sent broken syntax" from "you rejected my data".
        ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthenticated => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Locked => StatusCodes.Status423Locked,
        ErrorType.Failure => StatusCodes.Status500InternalServerError,

        // An unmapped member reaching here is a bug, not a client error. 500 is the honest answer:
        // we do not know what happened. The architecture test exists so this stays unreachable.
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Short human-readable title for a status code, used as the ProblemDetails title.</summary>
    /// <param name="errorType">The category to describe.</param>
    public static string ToTitle(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => "Validation failed",
        ErrorType.NotFound => "Resource not found",
        ErrorType.Conflict => "Conflict with current state",
        ErrorType.Unauthenticated => "Authentication required",
        ErrorType.Forbidden => "Access denied",
        ErrorType.Locked => "Account locked",
        ErrorType.Failure => "An unexpected error occurred",
        _ => "An unexpected error occurred",
    };

    /// <summary>Builds the stable problem type URN for an error code.</summary>
    /// <param name="errorCode">The error's stable code, for example <c>sample_record.not_found</c>.</param>
    public static string ToTypeUrn(string errorCode) => string.Concat(TypeUrnPrefix, errorCode);
}
