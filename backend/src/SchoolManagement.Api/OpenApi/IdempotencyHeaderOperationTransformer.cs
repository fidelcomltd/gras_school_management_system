using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SchoolManagement.Api.Idempotency;

namespace SchoolManagement.Api.OpenApi;

/// <summary>
/// Declares the <c>Idempotency-Key</c> request header and the <c>Idempotency-Replay</c> response
/// header on every operation whose endpoint carries <see cref="RequireIdempotencyKeyMarker"/> —
/// mirroring <see cref="CsrfHeaderOperationTransformer"/> in shape, for the same reason: the marker is
/// attached by <c>RequireIdempotencyKeyExtensions.RequireIdempotencyKey</c>, the SAME call that wires
/// the actual enforcement, so a route that declares the mechanism is documented correctly by
/// construction and a route that forgets it is (correctly) undocumented AND unprotected.
/// </summary>
/// <remarks>
/// This is the FIRST transformer to read <see cref="RequireIdempotencyKeyMarker"/> — TASK-0019 shipped
/// no route that calls <c>RequireIdempotencyKey</c> at all (contract-neutral by design), and
/// TASK-0027 (the card the marker's own remarks named as "the first to wire the transformer") is
/// still held for §5 sign-off. TASK-0005a's <c>PATCH /settings/identity</c> is therefore the actual
/// first caller, so this transformer exists now rather than later — the precise defect TASK-0003 was
/// reopened for, and TASK-0019 repeated, is an <c>Idempotency-Replay</c> response header that exists
/// only in prose (an endpoint description) and not as a declared header a generated client can see.
/// </remarks>
internal sealed class IdempotencyHeaderOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
        var marker = endpointMetadata?.OfType<RequireIdempotencyKeyMarker>().FirstOrDefault();

        if (marker is null)
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = RequireIdempotencyKeyExtensions.HeaderName,
            In = ParameterLocation.Header,
            Required = marker.Required,
            Description = marker.Required
                ? "Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, " +
                  "no whitespace. Required on this route."
                : "Client-generated key (UUID v4 recommended), 1-255 visible ASCII characters, " +
                  "no whitespace. Optional. A retry with the same key returns the stored response " +
                  $"unchanged and sets the `{RequireIdempotencyKeyExtensions.ReplayHeaderName}` " +
                  "response header, rather than repeating the request's effect.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        });

        // Declared on every response this operation lists, not only 200/201: a naive retry can just
        // as easily replay a prior FAILURE (a 409 stale-version conflict, say) as a prior success —
        // the stored response is whatever the first attempt actually produced.
        operation.Responses ??= new OpenApiResponses();

        foreach (var response in operation.Responses.Values.OfType<OpenApiResponse>())
        {
            response.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);

            response.Headers[RequireIdempotencyKeyExtensions.ReplayHeaderName] = new OpenApiHeader
            {
                Description =
                    "Present and set to \"true\" only when this response is a replay of a prior " +
                    "request that used the same Idempotency-Key, rather than a fresh execution.",
                Required = false,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            };
        }

        return Task.CompletedTask;
    }
}
