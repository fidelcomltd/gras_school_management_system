using Microsoft.AspNetCore.Http.HttpResults;
using SchoolManagement.Api.Http;
using SchoolManagement.Application.Abstractions.Identity;

namespace SchoolManagement.Api.Security;

/// <summary>
/// Marks an endpoint as CSRF-protected. Attached alongside the filter itself in
/// <see cref="CsrfEndpointFilterExtensions.RequireCsrfToken{TBuilder}"/>, so the enforcement and the
/// documentation can never drift apart — <c>SchoolManagement.Api.OpenApi.CsrfHeaderOperationTransformer</c>
/// reads this same marker to declare the <c>X-CSRF-Token</c> header in the OpenAPI document, rather
/// than a human re-annotating each operation by hand.
/// </summary>
internal sealed class RequireCsrfTokenMarker;

/// <summary>
/// Attaches the CSRF check to a mutating endpoint (approved contract delta §5): "CSRF token required
/// on every mutating request. Implemented once in the API layer, never per-endpoint" — this extension
/// IS that one implementation; every mutating <c>/auth/*</c> route calls it rather than re-deriving
/// the check.
/// </summary>
internal static class CsrfEndpointFilterExtensions
{
    /// <summary>The header a client echoes the CSRF cookie value into.</summary>
    public const string HeaderName = "X-CSRF-Token";

    /// <summary>Requires a valid, matching double-submit CSRF token.</summary>
    /// <param name="builder">The endpoint (or route group) to protect.</param>
    /// <param name="tolerateDeadSessionSubjectMismatch">
    /// Second-pass review MEDIUM 4, for <c>sign-out</c> ONLY: when the caller is not currently
    /// authenticated (no live session — including one that expired since the CSRF cookie was minted),
    /// accept any validly-signed, unexpired CSRF token regardless of which subject it names, instead
    /// of requiring it match the anonymous subject. See <see cref="CsrfTokenService.ValidateIgnoringSubject"/>
    /// for why this is safe specifically because sign-out is idempotent and non-privileged.
    /// </param>
    public static TBuilder RequireCsrfToken<TBuilder>(
        this TBuilder builder,
        bool tolerateDeadSessionSubjectMismatch = false)
        where TBuilder : IEndpointConventionBuilder
    {
        // Read by CsrfHeaderOperationTransformer to emit the matching OpenAPI parameter — approved
        // delta §5/§7: CSRF is documented as a required header parameter on each mutating operation,
        // not a second securityScheme. Attaching the marker HERE, next to the filter that actually
        // enforces it, is what keeps the document and the enforcement in lockstep: the next mutating
        // endpoint that calls RequireCsrfToken() is documented correctly with no separate step to
        // remember.
        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(new RequireCsrfTokenMarker()));

        builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var csrf = httpContext.RequestServices.GetRequiredService<CsrfTokenService>();
            var timeProvider = httpContext.RequestServices.GetRequiredService<TimeProvider>();
            var currentSession = httpContext.RequestServices.GetRequiredService<ICurrentSession>();

            httpContext.Request.Cookies.TryGetValue(AuthCookies.CsrfCookieName, out var cookieValue);
            var headerValue = httpContext.Request.Headers[HeaderName].ToString();

            if (string.IsNullOrEmpty(cookieValue) || string.IsNullOrEmpty(headerValue))
            {
                return CsrfProblem("csrf.missing", "A CSRF token is required for this request.");
            }

            var now = timeProvider.GetUtcNow();
            var sessionId = currentSession.SessionId;
            var expectedSubject = sessionId?.ToString() ?? CsrfTokenService.AnonymousSubject;

            var isValid = csrf.Validate(cookieValue, headerValue, expectedSubject, now);

            if (!isValid && tolerateDeadSessionSubjectMismatch && sessionId is null)
            {
                isValid = csrf.ValidateIgnoringSubject(cookieValue, headerValue, now);
            }

            if (!isValid)
            {
                return CsrfProblem("csrf.invalid", "The supplied CSRF token is missing, expired, or does not match.");
            }

            return await next(context).ConfigureAwait(false);
        });

        return builder;
    }

    private static ProblemHttpResult CsrfProblem(string errorCode, string detail) =>
        TypedResults.Problem(
            detail: detail,
            statusCode: StatusCodes.Status403Forbidden,
            title: "Access denied",
            type: ApiProblem.ToTypeUrn(errorCode),
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["errorCode"] = errorCode });
}
