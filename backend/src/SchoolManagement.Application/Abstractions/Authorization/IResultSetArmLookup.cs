namespace SchoolManagement.Application.Abstractions.Authorization;

/// <summary>
/// Resolves a result set to its arm, per spec 4.2.1: "A request naming a result set resolves to
/// that result set's arm."
/// </summary>
/// <remarks>
/// TASK-0076 dispatch A gave this a real implementation
/// (<c>SchoolManagement.Infrastructure.Authorization.ResultSetArmLookup</c>) now that <c>result_set</c>
/// exists. Still unreached by any real caller — no route declares
/// <see cref="ScopeParameterKind.ResultSet"/> yet, including dispatch B's score-sheet routes, which
/// scope by arm id directly (this card's contract delta).
/// </remarks>
public interface IResultSetArmLookup
{
    /// <summary>Returns the result set's arm, or <see langword="null"/> if it cannot be resolved.</summary>
    /// <param name="resultSetId">The result set to resolve.</param>
    /// <param name="cancellationToken">Propagated to any underlying query.</param>
    Task<Guid?> GetArmIdAsync(Guid resultSetId, CancellationToken cancellationToken);
}
