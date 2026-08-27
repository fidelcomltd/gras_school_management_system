namespace SchoolManagement.Application.Abstractions.Authorization;

/// <summary>
/// Resolves a result set to its arm, per spec 4.2.1: "A request naming a result set resolves to
/// that result set's arm."
/// </summary>
/// <remarks>
/// A SEAM — no results module exists yet, so no route uses
/// <see cref="ScopeParameterKind.ResultSet"/> in TASK-0002. Implement this against the results
/// module when it lands.
/// </remarks>
public interface IResultSetArmLookup
{
    /// <summary>Returns the result set's arm, or <see langword="null"/> if it cannot be resolved.</summary>
    /// <param name="resultSetId">The result set to resolve.</param>
    /// <param name="cancellationToken">Propagated to any underlying query.</param>
    Task<Guid?> GetArmIdAsync(Guid resultSetId, CancellationToken cancellationToken);
}
