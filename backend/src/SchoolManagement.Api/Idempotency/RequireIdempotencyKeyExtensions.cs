using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using SchoolManagement.Api.Http;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Idempotency;

namespace SchoolManagement.Api.Idempotency;

/// <summary>
/// Attached alongside the filter in <see cref="RequireIdempotencyKeyExtensions.RequireIdempotencyKey{TBuilder}"/>,
/// so enforcement and documentation cannot drift apart — the same construction
/// <c>RequireCsrfTokenMarker</c> uses. TASK-0019 shipped no route that declares it, so the committed
/// contract stayed unchanged then. TASK-0027 (admin accounts) was expected to be the first route to
/// call <see cref="RequireIdempotencyKeyExtensions.RequireIdempotencyKey{TBuilder}"/> for real, but it
/// remains held for §5 sign-off — TASK-0005a's <c>PATCH /settings/identity</c> shipped first instead.
/// <see cref="Api.OpenApi.IdempotencyHeaderOperationTransformer"/> is the transformer
/// that reads <see cref="Required"/> to declare the request header and the <c>Idempotency-Replay</c>
/// response header (orchestrator amendment A1).
/// </summary>
/// <param name="Required">
/// Whether <c>Idempotency-Key</c> is REQUIRED (absent → <c>idempotency.key_missing</c>) or merely
/// ACCEPTED (absent → the mechanism is skipped entirely) on this route.
/// </param>
internal sealed record RequireIdempotencyKeyMarker(bool Required);

/// <summary>
/// Attaches the idempotency mechanism to a mutating endpoint (TASK-0019, approved delta "TASK-0019 /
/// TASK-0027"): built once in the API layer, never per-endpoint — this extension IS that one
/// implementation, mirroring <c>CsrfEndpointFilterExtensions.RequireCsrfToken</c> in shape.
/// </summary>
internal static class RequireIdempotencyKeyExtensions
{
    /// <summary>The header a client supplies its idempotency key in.</summary>
    public const string HeaderName = "Idempotency-Key";

    /// <summary>Set on a genuine replay response — approved delta Part 1, orchestrator amendment A1.</summary>
    public const string ReplayHeaderName = "Idempotency-Replay";

    /// <summary>Caller identity recorded when the request carries no authenticated principal.</summary>
    private const string AnonymousCaller = "anonymous";

    // Visible ASCII, no whitespace, 1-255 characters — approved delta Part 1's exact format.
    private static readonly Regex KeyFormat = new(
        "^[\x21-\x7E]{1,255}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Requires or accepts an <c>Idempotency-Key</c> header, per <paramref name="required"/>.
    /// </summary>
    /// <param name="builder">The endpoint (or route group) to protect.</param>
    /// <param name="required">
    /// <see langword="true"/> when a retry could duplicate this route's effect (a genuine create) —
    /// absence is rejected. <see langword="false"/> when the route is already state-idempotent but
    /// benefits from replay/dedup (an edit or a status transition) — absence simply skips the
    /// mechanism.
    /// </param>
    public static TBuilder RequireIdempotencyKey<TBuilder>(this TBuilder builder, bool required)
        where TBuilder : IEndpointConventionBuilder
    {
        // Read by a future OpenAPI operation transformer (TASK-0027) — see the marker's remarks.
        builder.Add(endpointBuilder =>
            endpointBuilder.Metadata.Add(new RequireIdempotencyKeyMarker(required)));

        builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var headerValue = httpContext.Request.Headers[HeaderName].ToString();

            if (string.IsNullOrEmpty(headerValue))
            {
                if (!required)
                {
                    return await next(context).ConfigureAwait(false);
                }

                return Problem(
                    StatusCodes.Status400BadRequest,
                    "idempotency.key_missing",
                    "An Idempotency-Key header is required for this request.");
            }

            if (!KeyFormat.IsMatch(headerValue))
            {
                return Problem(
                    StatusCodes.Status400BadRequest,
                    "idempotency.key_malformed",
                    "The Idempotency-Key header must be 1-255 visible ASCII characters with no whitespace.");
            }

            var services = httpContext.RequestServices;
            var currentUser = services.GetRequiredService<ICurrentUser>();
            var jsonOptions = services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
            var store = services.GetRequiredService<IIdempotencyStore>();
            var idempotencyOptions = services.GetRequiredService<IOptions<IdempotencyOptions>>().Value;
            var timeProvider = services.GetRequiredService<TimeProvider>();

            var caller = currentUser.UserId ?? AnonymousCaller;
            var command = context.Arguments.OfType<IBaseCommand>().FirstOrDefault();
            var uploadedFile = context.Arguments.OfType<IFormFile>().FirstOrDefault();
            var fingerprint = await BuildFingerprintAsync(
                httpContext, caller, command, uploadedFile, jsonOptions, httpContext.RequestAborted).ConfigureAwait(false);

            var now = timeProvider.GetUtcNow();
            var retention = TimeSpan.FromHours(idempotencyOptions.RetentionHours);

            var claim = await store
                .TryClaimAsync(headerValue, caller, fingerprint, now, retention, httpContext.RequestAborted)
                .ConfigureAwait(false);

            switch (claim.Outcome)
            {
                case IdempotencyClaimOutcome.Conflict:
                    return Problem(
                        StatusCodes.Status409Conflict,
                        "idempotency.key_conflict",
                        "This idempotency key was already used with a different request.");

                case IdempotencyClaimOutcome.InProgress:
                    return Problem(
                        StatusCodes.Status409Conflict,
                        "idempotency.request_in_progress",
                        "A request with this idempotency key is already being processed. Retry shortly.");

                case IdempotencyClaimOutcome.Replay:
                    httpContext.Response.Headers[ReplayHeaderName] = "true";
                    return new IdempotencyReplayResult(claim.Replay!);

                case IdempotencyClaimOutcome.Claimed:
                default:
                    break;
            }

            var result = await next(context).ConfigureAwait(false);
            var captured = CaptureResponse(result, jsonOptions);

            await store.CompleteAsync(headerValue, caller, captured, now, httpContext.RequestAborted)
                .ConfigureAwait(false);

            return result;
        });

        return builder;
    }

    /// <summary>
    /// Method + path + caller + the bound command serialised with the app's own JSON options, plus
    /// (TASK-0005b) a SHA-256 of any uploaded file's bytes — a reused key submitted against a
    /// different payload OR different bytes changes this string and is therefore classified
    /// <c>idempotency.key_conflict</c>, never replayed against an unrelated response.
    /// </summary>
    /// <remarks>
    /// <paramref name="uploadedFile"/> is read here to compute the hash and then left exactly as it
    /// was: <c>IFormFile.OpenReadStream()</c> reopens the underlying buffered form data from the
    /// start on every call, so the endpoint's own later read (which builds the actual command) is
    /// unaffected. Before this method existed, a multipart body had NO command argument at all (the
    /// endpoint binds a bare <see cref="IFormFile"/>, not an <see cref="IBaseCommand"/>), so the
    /// fingerprint for every upload collapsed to the same method/path/caller triple regardless of the
    /// file's content — the gap 0005b's approved delta named.
    /// </remarks>
    internal static async Task<string> BuildFingerprintAsync(
        HttpContext httpContext,
        string caller,
        IBaseCommand? command,
        IFormFile? uploadedFile,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var bodyPart = command is null
            ? string.Empty
            : JsonSerializer.Serialize(command, command.GetType(), jsonOptions);

        var filePart = string.Empty;

        if (uploadedFile is not null)
        {
            var stream = uploadedFile.OpenReadStream();
            var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            filePart = Convert.ToHexString(hash);
        }

        return string.Join('|', httpContext.Request.Method, httpContext.Request.Path.Value, caller, bodyPart, filePart);
    }

    /// <summary>
    /// Extracts (status code, content type, body, <c>Location</c>) from whatever the protected
    /// handler returned, and REDACTS any property marked
    /// <see cref="RedactFromIdempotencyReplayAttribute"/> out of the copy that gets stored — the
    /// live <paramref name="result"/> returned to the actual caller is never touched.
    /// </summary>
    private static IdempotencyStoredResponse CaptureResponse(object? result, JsonSerializerOptions jsonOptions)
    {
        var statusCode = (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;
        var contentType = (result as IContentTypeHttpResult)?.ContentType;
        var value = (result as IValueHttpResult)?.Value;

        // No shared interface carries `Location` (TypedResults.Created<T> and its siblings each
        // declare their own property of that name) — reflection is the same accepted idiom
        // ApplicationDbContext already uses for generic, attribute/shape-driven model traversal.
        var location = result?.GetType().GetProperty("Location")?.GetValue(result) as string;

        string? bodyJson = null;

        if (value is not null)
        {
            var node = JsonSerializer.SerializeToNode(value, value.GetType(), jsonOptions);
            Redact(node, value.GetType(), jsonOptions);
            bodyJson = node?.ToJsonString(jsonOptions);
        }

        return new IdempotencyStoredResponse(statusCode, contentType, bodyJson, location);
    }

    private static void Redact(JsonNode? node, Type valueType, JsonSerializerOptions jsonOptions)
    {
        if (node is not JsonObject jsonObject)
        {
            return;
        }

        foreach (var property in valueType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<RedactFromIdempotencyReplayAttribute>() is null)
            {
                continue;
            }

            var jsonName = property.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name
                ?? jsonOptions.PropertyNamingPolicy?.ConvertName(property.Name)
                ?? property.Name;

            if (jsonObject.ContainsKey(jsonName))
            {
                jsonObject[jsonName] = null;
            }
        }
    }

    private static ProblemHttpResult Problem(int statusCode, string errorCode, string detail) =>
        TypedResults.Problem(
            detail: detail,
            statusCode: statusCode,
            title: statusCode == StatusCodes.Status409Conflict ? "Conflict with current state" : "Bad request",
            type: ApiProblem.ToTypeUrn(errorCode),
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["errorCode"] = errorCode });

    /// <summary>Replays a stored response verbatim: same status, content type, body and <c>Location</c>.</summary>
    private sealed class IdempotencyReplayResult(IdempotencyStoredResponse response) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = response.StatusCode;

            if (!string.IsNullOrEmpty(response.Location))
            {
                httpContext.Response.Headers.Location = response.Location;
            }

            if (response.BodyJson is null)
            {
                return Task.CompletedTask;
            }

            httpContext.Response.ContentType = response.ContentType ?? "application/json";
            return httpContext.Response.WriteAsync(response.BodyJson);
        }
    }
}
