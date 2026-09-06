using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Persistence port for the <see cref="SchoolProfile"/> singleton. Implemented in Infrastructure.</summary>
/// <remarks>
/// Both members return a non-nullable <see cref="SchoolProfile"/>: the row is guaranteed to exist
/// from the moment the migration runs (<c>SchoolProfileConfiguration.HasData</c>), so its absence is
/// a genuine data-integrity defect, not an expected outcome a handler branches on — that failure mode
/// is left to throw and become a 500, the same way a broken migration would surface anywhere else.
/// </remarks>
public interface ISchoolProfileRepository
{
    /// <summary>
    /// Loads the singleton row TRACKED, for a command that will mutate and save it.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<SchoolProfile> GetTrackedSingletonAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads the singleton row read-only (<c>AsNoTracking</c>), for a query.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<SchoolProfile> GetReadOnlySingletonAsync(CancellationToken cancellationToken);
}
