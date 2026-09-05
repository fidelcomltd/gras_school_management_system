using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SchoolManagement.Api.Security;

namespace SchoolManagement.Api.OpenApi;

/// <summary>
/// Declares the required <c>X-CSRF-Token</c> header on every operation whose endpoint carries
/// <see cref="RequireCsrfTokenMarker"/> (approved contract delta §5/§7): "CSRF is documented as a
/// required <c>X-CSRF-Token</c> header parameter on each mutating operation, not a second
/// securityScheme."
/// </summary>
/// <remarks>
/// <para>
/// WHY A TRANSFORMER READING ENDPOINT METADATA, NOT FOUR HAND-WRITTEN ANNOTATIONS: the marker is
/// attached by <c>CsrfEndpointFilterExtensions.RequireCsrfToken</c> — the SAME call that wires the
/// actual enforcement. A future mutating endpoint that calls <c>.RequireCsrfToken()</c> is documented
/// correctly by construction; one that forgets to call it is (correctly) undocumented AND
/// unprotected, rather than documented-but-unprotected or protected-but-undocumented, which is what a
/// hand-annotated list risks the moment the two lists drift.
/// </para>
/// <para>
/// Deliberately a PARAMETER, not a second <c>securityScheme</c> — OpenAPI's <c>security</c> concept
/// answers "who is calling", and the CSRF header proves request provenance, not identity. See
/// <see cref="AuthSecuritySchemeDocumentTransformer"/> for the one security scheme this API declares.
/// </para>
/// </remarks>
internal sealed class CsrfHeaderOperationTransformer : IOpenApiOperationTransformer
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

        if (endpointMetadata is null || !endpointMetadata.OfType<RequireCsrfTokenMarker>().Any())
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = CsrfEndpointFilterExtensions.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description =
                "The value of the __Host-XSRF-TOKEN cookie, echoed verbatim (double-submit CSRF, " +
                "approved contract delta §5). Obtain it from GET /auth/csrf or from a prior " +
                "response's Set-Cookie.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        });

        return Task.CompletedTask;
    }
}
