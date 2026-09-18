using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// Persistence port for the <see cref="ResultRules"/> singleton (spec 6.2.8). Implemented in
/// Infrastructure. Same shape as <see cref="ISchoolProfileRepository"/> — this is the port the
/// computation engine reads from too, once it exists.
/// </summary>
/// <remarks>
/// Both members return a non-nullable <see cref="ResultRules"/>: the row is guaranteed to exist from
/// the moment the migration runs (<c>ResultRulesConfiguration.HasData</c>), so its absence is a
/// genuine data-integrity defect, not an expected outcome a handler branches on.
/// </remarks>
public interface IResultRulesRepository
{
    /// <summary>Loads the singleton row TRACKED, for a command that will mutate and save it.</summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<ResultRules> GetTrackedSingletonAsync(CancellationToken cancellationToken);

    /// <summary>Loads the singleton row read-only (<c>AsNoTracking</c>), for a query.</summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<ResultRules> GetReadOnlySingletonAsync(CancellationToken cancellationToken);
}
