using Microsoft.AspNetCore.Http.HttpResults;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Api.Http;

/// <summary>
/// Converts a <see cref="Result"/> into an HTTP response. The single bridge between the application's
/// outcome model and the HTTP surface.
/// </summary>
/// <remarks>
/// <para>
/// An endpoint's whole body should be: send the request, then <c>Match</c> the result. It names the
/// SUCCESS shape only — 200 with a body, 201 with a Location, 204 — and every failure path is derived
/// from the error's <see cref="ErrorType"/> by <see cref="ApiProblem"/>. An endpoint that writes its
/// own error response is a review blocker.
/// </para>
/// <para>
/// Returns <see cref="IResult"/> rather than a concrete <c>TypedResults</c> type because the two
/// branches produce different types. To keep the OpenAPI document accurate the endpoint declares its
/// responses with <c>.Produces&lt;T&gt;(...)</c> / <c>.ProducesProblem(...)</c> metadata — see
/// <c>ReferenceEndpoints</c>.
/// </para>
/// </remarks>
internal static class ResultExtensions
{
    /// <summary>
    /// Produces <paramref name="onSuccess"/> for a success, or an RFC 9457 problem response for a
    /// failure.
    /// </summary>
    /// <typeparam name="TValue">The success payload type.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <param name="onSuccess">
    /// Builds the success response from the payload. Only reached when the result succeeded, so it may
    /// use <c>Value</c> freely.
    /// </param>
    public static IResult Match<TValue>(this Result<TValue> result, Func<TValue, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess(result.Value) : ToProblem(result.Error);
    }

    /// <summary>
    /// Produces <paramref name="onSuccess"/> for a success, or an RFC 9457 problem response for a
    /// failure. For commands with no payload.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <param name="onSuccess">Builds the success response.</param>
    public static IResult Match(this Result result, Func<IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess() : ToProblem(result.Error);
    }

    /// <summary>
    /// Renders an <see cref="Error"/> as a ProblemDetails response.
    /// </summary>
    /// <remarks>
    /// <c>traceId</c> is NOT added here. It is attached centrally by
    /// <c>CustomizeProblemDetails</c> in <c>ProblemDetailsConfiguration</c>, so that every problem
    /// response carries it — including the ones produced by the framework (a 401 from the auth
    /// middleware, a 400 from model binding) that never pass through this method.
    /// </remarks>
    private static ProblemHttpResult ToProblem(Error error)
    {
        var statusCode = ApiProblem.ToStatusCode(error.Type);

        // Per-field messages travel in the standard "errors" member so a client can attach them to
        // the right form fields, rather than having to parse a human-readable sentence.
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["errorCode"] = error.Code,
        };

        if (error is ValidationError validationError)
        {
            extensions["errors"] = validationError.Failures;
        }

        if (error is LockedError lockedError)
        {
            extensions["lockedUntil"] = lockedError.LockedUntilUtc;
        }

        // Spec 6.2.12: "Returns 422 with the single first failure and the offending band index" — so
        // the grading editor can highlight the offending row without re-parsing the message text.
        if (error is GradingBandValidationError gradingBandError)
        {
            extensions["bandIndex"] = gradingBandError.BandIndex;
        }

        // Same reasoning as GradingBandValidationError above, extended to a second dimension: which
        // scale, and — where the failure is about one point — which point within it (spec 6.2.13).
        if (error is RatingScaleValidationError ratingScaleError)
        {
            extensions["scaleIndex"] = ratingScaleError.ScaleIndex;
            extensions["pointIndex"] = ratingScaleError.PointIndex;
        }

        // Same reasoning as RatingScaleValidationError above, for spec 6.2.13's development domains:
        // which domain, and — where the failure is about one indicator — which indicator within it.
        if (error is DevelopmentDomainValidationError developmentDomainError)
        {
            extensions["domainIndex"] = developmentDomainError.DomainIndex;
            extensions["indicatorIndex"] = developmentDomainError.IndicatorIndex;
        }

        // TASK-0088 stage B: the contract's first typed problem extension — spec 6.7.5's "422 with a
        // structured list of what is missing", the SAME body GET /arms/{armId}/readiness returns (AC
        // B6). Documented as ResultSetNotReadyProblemDetails (see its own remarks); this is the runtime
        // half, an ordinary extension member exactly like errorCode/traceId/lockedUntil above.
        if (error is ResultSetNotReadyError notReadyError)
        {
            extensions["readiness"] = notReadyError.Readiness;
        }

        return TypedResults.Problem(
            detail: error.Description,
            statusCode: statusCode,
            title: ApiProblem.ToTitle(error.Type),
            type: ApiProblem.ToTypeUrn(error.Code),
            extensions: extensions);
    }
}
