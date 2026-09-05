namespace SchoolManagement.Domain.Common;

/// <summary>
/// The category of a failure. This is the ONLY thing that decides an HTTP status code:
/// the mapping lives in exactly one place (SchoolManagement.Api.Http.ResultExtensions)
/// so a handler never picks a status code and two endpoints can never disagree.
/// </summary>
/// <remarks>
/// Adding a member here is a contract change: update the central mapping and the
/// <c>ErrorTypeMappingTests</c>, which fail on any unmapped member.
/// </remarks>
public enum ErrorType
{
    /// <summary>An unexpected failure. Maps to HTTP 500.</summary>
    Failure = 0,

    /// <summary>Request data was rejected by a validator. Maps to HTTP 422.</summary>
    Validation = 1,

    /// <summary>The requested resource does not exist. Maps to HTTP 404.</summary>
    NotFound = 2,

    /// <summary>The request conflicts with current state. Maps to HTTP 409.</summary>
    Conflict = 3,

    /// <summary>The caller is not authenticated. Maps to HTTP 401.</summary>
    Unauthenticated = 4,

    /// <summary>The caller is authenticated but not permitted. Maps to HTTP 403.</summary>
    Forbidden = 5,

    /// <summary>
    /// The account is locked (spec 6.1.11). Maps to HTTP 423. TASK-0003: fires only when the
    /// submitted password was correct — see <c>SignInCommandHandler</c>.
    /// </summary>
    Locked = 6,
}
