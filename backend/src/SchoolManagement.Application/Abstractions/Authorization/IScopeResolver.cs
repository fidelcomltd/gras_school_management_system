namespace SchoolManagement.Application.Abstractions.Authorization;

/// <summary>
/// Implements the server-side scope resolution rules of spec 4.2.1, turning a route's declared
/// scope-parameter kind and raw value into a <see cref="ScopeResolution"/> the privilege check can
/// act on.
/// </summary>
public interface IScopeResolver
{
    /// <summary>Resolves one route's scope target.</summary>
    /// <param name="kind">What <paramref name="parameterValue"/> names.</param>
    /// <param name="parameterValue">
    /// The raw route-parameter value, or <see langword="null"/> if the route carried none (an
    /// unparsable or absent value is treated identically to a missing one — both fail closed).
    /// </param>
    /// <param name="cancellationToken">Propagated to any underlying lookup.</param>
    Task<ScopeResolution> ResolveAsync(
        ScopeParameterKind kind,
        Guid? parameterValue,
        CancellationToken cancellationToken);
}
