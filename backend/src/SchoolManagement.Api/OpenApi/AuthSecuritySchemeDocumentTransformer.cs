using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SchoolManagement.Api.OpenApi;

/// <summary>
/// Declares the <c>CookieSession</c> security scheme (approved contract delta §7) and attaches it to
/// <c>me</c>, <c>refresh</c> and <c>password</c> — the three operations behind
/// <c>RequireAuthenticatedCaller()</c>/<c>RequirePrivilege</c>, never <c>sign-in</c>, <c>sign-out</c>
/// or <c>csrf</c>, which are anonymous/optional.
/// </summary>
/// <remarks>
/// CSRF is deliberately NOT modelled as a second security scheme here — OpenAPI's <c>security</c>
/// concept answers "who is calling", and the CSRF header proves request provenance, not identity
/// (approved delta §7). It is documented as an ordinary required header parameter on each mutating
/// <c>/auth/*</c> operation instead — see <c>AuthEndpoints</c>'s <c>.WithOpenApiCsrfHeader()</c>.
/// </remarks>
internal sealed class AuthSecuritySchemeDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>The security scheme's name, matching <see cref="Security.AuthCookies.SessionCookieName"/>.</summary>
    public const string SchemeName = "CookieSession";

    private static readonly string[] GatedOperationIds = ["GetMe", "RefreshSession", "ChangePassword"];

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = Security.AuthCookies.SessionCookieName,
            Description =
                "Opaque session token set by POST /api/v1/auth/sign-in. HttpOnly — the browser " +
                "attaches it automatically; it is never readable or settable from JavaScript.",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes[SchemeName] = scheme;

        if (document.Paths is null)
        {
            return Task.CompletedTask;
        }

        var schemeReference = new OpenApiSecuritySchemeReference(SchemeName, document);

        foreach (var pathItem in document.Paths.Values)
        {
            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations.Values)
            {
                if (operation.OperationId is null || !GatedOperationIds.Contains(operation.OperationId, StringComparer.Ordinal))
                {
                    continue;
                }

                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [schemeReference] = [],
                });
            }
        }

        return Task.CompletedTask;
    }
}
