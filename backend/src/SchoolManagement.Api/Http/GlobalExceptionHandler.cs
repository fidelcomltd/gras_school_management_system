using Microsoft.AspNetCore.Diagnostics;
using SchoolManagement.Application.Abstractions.Persistence;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Api.Http;

/// <summary>
/// Last line of defence: turns any unhandled exception into an RFC 9457 problem response.
/// </summary>
/// <remarks>
/// <para>
/// Registered via <c>AddExceptionHandler</c> + <c>UseExceptionHandler</c>. Because this exists,
/// handlers and endpoints never need a try/catch — and should not have one. A <c>catch</c> in a
/// handler is almost always a bug being hidden.
/// </para>
/// <para>
/// SECURITY: outside Development the response contains NO exception message, type name, or stack
/// trace. Those reveal library versions, file paths and schema details, and exception messages
/// routinely contain user data. The client gets a status code, a stable error code and a
/// <c>traceId</c>; the full detail goes to the log, correlated by that same ID.
/// </para>
/// </remarks>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger,
    IPersistenceErrorTranslator persistenceErrorTranslator)
    : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // The client has gone. Writing a response would throw again, and this is not an error worth
        // reporting — it is someone closing a browser tab.
        if (httpContext.RequestAborted.IsCancellationRequested && exception is OperationCanceledException)
        {
            // CA1873: `httpContext.Request.Path` is a PathString, so passing it where the
            // [LoggerMessage] method expects `string` invokes PathString's implicit conversion —
            // an evaluation the analyzer wants skipped when Debug logging (this call's level) is
            // disabled. Guarded and hoisted to a local, same shape as the rest of this file.
            if (logger.IsEnabled(LogLevel.Debug))
            {
                string requestPath = httpContext.Request.Path;
                ApiLog.RequestAborted(logger, requestPath);
            }

            return true;
        }

        // The framework's own request-level rejections. ASP.NET Core signals "the CLIENT sent something
        // unusable" by throwing BadHttpRequestException with the intended status code attached — 400 for
        // unparseable JSON or an unknown member, 413 for a body over the size limit.
        //
        // Honouring StatusCode matters: without this branch every one of these became a 500. A client
        // sending malformed JSON would be told the SERVER had failed, and each occurrence would count
        // against the service's error rate and page somebody.
        if (exception is BadHttpRequestException badRequest)
        {
            // CA1873 — see the RequestAborted guard above for why this needs both the guard and the
            // explicitly-typed local (PathString's implicit conversion to string).
            if (logger.IsEnabled(LogLevel.Information))
            {
                string requestPath = httpContext.Request.Path;
                ApiLog.RejectedMalformedRequest(logger, requestPath, badRequest.StatusCode);
            }

            return await WriteProblemAsync(
                httpContext,
                statusCode: badRequest.StatusCode,
                title: badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "Request too large"
                    : "Malformed request",
                // Deliberately generic. The framework's message can quote the offending JSON fragment,
                // which is caller-supplied data and may contain anything.
                detail: badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "The request body exceeded the maximum permitted size."
                    : "The request body could not be read. Check that it is valid JSON and contains only known fields.",
                errorCode: badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "request.too_large"
                    : "request.malformed",
                includeExceptionDetail: environment.IsDevelopment()).ConfigureAwait(false);
        }

        // A recognised database constraint failure becomes the same 4xx a handler would have returned.
        // Anything else is an unexpected fault.
        var translated = persistenceErrorTranslator.TryTranslate(exception);

        if (translated is not null)
        {
            ApiLog.TranslatedPersistenceFailure(logger, translated.Code, exception);

            return await WriteProblemAsync(
                httpContext,
                ApiProblem.ToStatusCode(translated.Type),
                ApiProblem.ToTitle(translated.Type),
                translated.Description,
                translated.Code,
                includeExceptionDetail: false).ConfigureAwait(false);
        }

        // CA1873 — see the RequestAborted guard above for why this needs both the guard and the
        // explicitly-typed local (PathString's implicit conversion to string).
        if (logger.IsEnabled(LogLevel.Error))
        {
            string requestPath = httpContext.Request.Path;
            ApiLog.UnhandledException(logger, requestPath, exception);
        }

        var failure = Error.Failure(
            "server.unexpected_error",
            "An unexpected error occurred. Quote the traceId when reporting this.");

        return await WriteProblemAsync(
            httpContext,
            ApiProblem.ToStatusCode(failure.Type),
            ApiProblem.ToTitle(failure.Type),
            failure.Description,
            failure.Code,
            includeExceptionDetail: environment.IsDevelopment()).ConfigureAwait(false);

        async ValueTask<bool> WriteProblemAsync(
            HttpContext context,
            int statusCode,
            string title,
            string detail,
            string errorCode,
            bool includeExceptionDetail)
        {
            context.Response.StatusCode = statusCode;

            var extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["errorCode"] = errorCode,
            };

            if (includeExceptionDetail)
            {
                // DEVELOPMENT ONLY — see the class remarks.
                extensions["exception"] = new
                {
                    type = exception.GetType().FullName,
                    message = exception.Message,
                    stackTrace = exception.StackTrace,
                };
            }

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                Exception = exception,
                ProblemDetails =
                {
                    Status = statusCode,
                    Title = title,
                    Detail = detail,
                    Type = ApiProblem.ToTypeUrn(errorCode),
                    Extensions = extensions,
                },
            }).ConfigureAwait(false);
        }
    }
}
