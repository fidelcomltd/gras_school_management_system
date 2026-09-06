using Asp.Versioning;
using Microsoft.AspNetCore.OpenApi;
// Microsoft.OpenApi 2.x (which .NET 10 ships with) flattened its object model out of
// Microsoft.OpenApi.Models into the root namespace, and replaced the OperationType enum with
// System.Net.Http.HttpMethod as the key of PathItem.Operations. Code written against 1.x will not
// compile here.
using Microsoft.OpenApi;

namespace SchoolManagement.Api.OpenApi;

/// <summary>
/// OpenAPI document generation.
/// </summary>
/// <remarks>
/// <para>
/// The document is generated from CODE — endpoint metadata, DTO types and XML doc comments. It is never
/// hand-edited, and <c>../contracts/openapi.json</c> is build OUTPUT that only the documented release
/// command may write. See README, "Regenerating the contract".
/// </para>
/// <para>
/// Because the frontend generates a typed client from that document, an inaccuracy here becomes a
/// compile error or a runtime bug over there. That is why endpoints must declare every response type.
/// </para>
/// </remarks>
public static class OpenApiSetup
{
    /// <summary>The single document name. A future v2 gets its own document.</summary>
    public const string DocumentName = "v1";

    /// <summary>The API version this document describes.</summary>
    public const string ApiVersionString = "1.0";

    /// <summary>The concrete URL segment substituted for the version route template.</summary>
    public const string UrlVersionSegment = "v1";

    /// <summary>Adds OpenAPI document generation.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApiOpenApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // XML doc comments are picked up automatically: Microsoft.AspNetCore.OpenApi in .NET 10 runs a
        // source generator over the XML documentation file, which is why GenerateDocumentationFile is
        // true repo-wide and CS1591 is an ERROR in Api and Application. A missing /// on a contract type
        // is a missing description in the document, so the compiler enforces it.
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer<ApiInfoDocumentTransformer>();
            options.AddDocumentTransformer<VersionedPathDocumentTransformer>();
            options.AddDocumentTransformer<AuthSecuritySchemeDocumentTransformer>();
            options.AddOperationTransformer<CsrfHeaderOperationTransformer>();
            options.AddOperationTransformer<IdempotencyHeaderOperationTransformer>();
            options.AddSchemaTransformer<SchemaExampleTransformer>();
            options.AddSchemaTransformer<ProblemDetailsSchemaTransformer>();
        });

        return services;
    }

    /// <summary>Configures API versioning: URL segment, as decided in <c>docs/adr/0003</c>.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApiVersioningScheme(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);

            // The version is in the path, so it is always present in practice. This keeps a request to
            // an unversioned path (a health probe, a misconfigured proxy) resolving rather than 400ing.
            options.AssumeDefaultVersionWhenUnspecified = true;

            // Emits api-supported-versions / api-deprecated-versions response headers, which is how a
            // client discovers that the version it is pinned to is going away.
            options.ReportApiVersions = true;

            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        return services;
    }
}

/// <summary>
/// Sets the document's <c>info</c> block.
/// </summary>
internal sealed class ApiInfoDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Info = new OpenApiInfo
        {
            Title = "School Management API",
            Version = OpenApiSetup.ApiVersionString,
            Description =
                "REST API for the School Management service.\n\n" +
                "**Conventions**\n\n" +
                "- Errors use RFC 9457 `application/problem+json`. Every error body carries a stable " +
                "`errorCode` and a `traceId`; branch on `errorCode`, never on the human-readable " +
                "`detail`.\n" +
                "- Validation failures return **422** with a `errors` object keyed by property name. " +
                "A **400** means the request itself was malformed (unparseable JSON, wrong query " +
                "parameter type).\n" +
                "- Timestamps are UTC ISO-8601 with an explicit offset. Convert to a local zone in the " +
                "UI only.\n" +
                "- IDs are opaque strings. Do not parse them or assume a format.\n" +
                "- Collections are always paginated in the standard envelope. `pageSize` is capped at " +
                "100 and a larger value is rejected, not silently reduced.\n" +
                "- Enums cross the wire as strings. Tolerate members you do not recognise rather than " +
                "failing to deserialise.\n" +
                "- Unknown fields in a request body are REJECTED with **400**, not ignored — a " +
                "misspelled field is reported rather than silently dropped.\n" +
                "- An unknown path returns **401**, not 404. Endpoints are protected by default and the " +
                "policy applies to unmatched routes too, so route existence cannot be probed without " +
                "authenticating. A 401 on a URL you believe exists may simply be a typo.\n\n" +
                "**Authentication is not yet configured.** Endpoints are protected by default; while " +
                "the identity provider decision is outstanding, protected endpoints return 401 for " +
                "every caller. See `docs/ASSUMPTIONS.md` in the repository.",
        };

        return Task.CompletedTask;
    }
}

/// <summary>
/// Replaces the <c>v{version}</c> route template in paths with the concrete version segment.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS IS NECESSARY: URL-segment versioning routes on the template
/// <c>/api/v{version:apiVersion}</c>, and the generator faithfully emits that template as the path. A
/// client generated from it would produce methods taking a <c>version</c> argument, or worse, call the
/// literal URL <c>/api/v%7Bversion%7D/...</c>. Since this document describes exactly one version, the
/// honest path is the concrete one.
/// </para>
/// <para>
/// The matching <c>version</c> path PARAMETER is also removed, for the same reason: after substitution
/// it no longer corresponds to anything in the path, and a generator would emit a required argument
/// that must always be the same value.
/// </para>
/// </remarks>
internal sealed class VersionedPathDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string VersionTemplate = "v{version}";
    private const string VersionParameterName = "version";

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Paths is null)
        {
            return Task.CompletedTask;
        }

        var rewritten = new OpenApiPaths();

        foreach (var (path, pathItem) in document.Paths)
        {
            RemoveVersionParameter(pathItem);

            var concretePath = path.Replace(
                VersionTemplate,
                OpenApiSetup.UrlVersionSegment,
                StringComparison.Ordinal);

            rewritten[concretePath] = pathItem;
        }

        document.Paths = rewritten;

        return Task.CompletedTask;
    }

    private static void RemoveVersionParameter(IOpenApiPathItem pathItem)
    {
        if (pathItem.Operations is null)
        {
            return;
        }

        foreach (var operation in pathItem.Operations.Values)
        {
            if (operation.Parameters is null)
            {
                continue;
            }

            for (var index = operation.Parameters.Count - 1; index >= 0; index--)
            {
                var parameter = operation.Parameters[index];

                if (parameter.In == ParameterLocation.Path &&
                    string.Equals(parameter.Name, VersionParameterName, StringComparison.Ordinal))
                {
                    operation.Parameters.RemoveAt(index);
                }
            }
        }
    }
}
