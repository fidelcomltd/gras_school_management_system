namespace SchoolManagement.Api.Security;

/// <summary>Attaches an explicit, reasoned opt-out from <see cref="CsrfDeclarationGuard"/>.</summary>
internal sealed record CsrfExemptMarker(string Reason);

/// <summary>Attaches <see cref="CsrfExemptMarker"/>, for the rare mutating route that genuinely needs no CSRF check.</summary>
internal static class CsrfExemptionExtensions
{
    /// <summary>Exempts the endpoint from <see cref="CsrfDeclarationGuard"/>, recording why.</summary>
    /// <param name="builder">The endpoint (or route group) to exempt.</param>
    /// <param name="reason">A human-readable reason. Required — this is a deliberate decision, not a default.</param>
    public static TBuilder ExemptFromCsrfRequirement<TBuilder>(this TBuilder builder, string reason)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(new CsrfExemptMarker(reason)));
        return builder;
    }
}

/// <summary>
/// THE BOOT-TIME GUARD for CSRF, modelled on <see cref="PrivilegeDeclarationGuard"/>. Root CLAUDE.md
/// §5 / approved contract delta §5: "CSRF token required on every mutating request... never
/// per-feature." <c>RequireCsrfToken()</c> being callable per-endpoint made it a forgettable opt-IN;
/// this makes forgetting it a startup failure instead of a live hole — exactly what
/// <c>POST /reference/records</c> (a mutating scaffold endpoint with no CSRF check at all) proved was
/// already happening.
/// </summary>
/// <remarks>
/// A mutating route (POST/PUT/PATCH/DELETE) passes if it carries
/// <see cref="RequireCsrfTokenMarker"/> (attached by
/// <see cref="CsrfEndpointFilterExtensions.RequireCsrfToken{TBuilder}"/>) or an explicit
/// <see cref="CsrfExemptMarker"/> (attached by
/// <see cref="CsrfExemptionExtensions.ExemptFromCsrfRequirement{TBuilder}"/>, reason required). GET/
/// HEAD/OPTIONS routes are outside this guard's scope — CSRF is a mutation concern.
/// </remarks>
internal static class CsrfDeclarationGuard
{
    private static readonly HashSet<string> MutatingMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    /// <summary>Validates every mutating endpoint currently mapped onto <paramref name="endpoints"/>.</summary>
    /// <exception cref="InvalidOperationException">
    /// One or more mutating routes declare neither <see cref="RequireCsrfTokenMarker"/> nor
    /// <see cref="CsrfExemptMarker"/>. The message names every offending route.
    /// </exception>
    public static void Validate(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var undeclared = endpoints.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(IsMutating)
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<RequireCsrfTokenMarker>() is null &&
                endpoint.Metadata.GetMetadata<CsrfExemptMarker>() is null)
            .Select(Describe)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (undeclared.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "The following mutating routes declare no CSRF check and no explicit exemption: " +
            string.Join("; ", undeclared) +
            ". Call RequireCsrfToken() (approved contract delta §5), or ExemptFromCsrfRequirement" +
            "(reason) if the route genuinely needs none.");
    }

    private static bool IsMutating(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
            .Any(method => MutatingMethods.Contains(method)) == true;

    private static string Describe(RouteEndpoint endpoint) =>
        endpoint.DisplayName ?? endpoint.RoutePattern.RawText ?? "(unnamed route)";
}
