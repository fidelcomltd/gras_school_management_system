using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Authorization;

/// <summary>
/// The real <see cref="IResultSetArmLookup"/> (TASK-0076 dispatch A): resolves a result set to its
/// arm with a direct query against <c>result_set</c>, per spec 4.2.1: "A request naming a result set
/// resolves to that result set's arm."
/// </summary>
/// <remarks>
/// Replaces <c>NotYetImplementedResultSetArmLookup</c>, which threw because no results module
/// existed yet — the same shape <c>PupilArmOfRecordLookup</c> took over
/// <c>NotYetImplementedPupilArmOfRecordLookup</c> at TASK-0059. No route declares
/// <see cref="ScopeParameterKind.ResultSet"/> yet (dispatch B carries no such route either — see this
/// card's contract delta), so this remains unreached by any real caller today; it is simply no
/// longer a throwing stub.
/// </remarks>
internal sealed class ResultSetArmLookup(ApplicationDbContext context) : IResultSetArmLookup
{
    /// <inheritdoc />
    public async Task<Guid?> GetArmIdAsync(Guid resultSetId, CancellationToken cancellationToken) =>
        await context.ResultSets.AsNoTracking()
            .Where(resultSet => resultSet.Id == resultSetId)
            .Select(resultSet => (Guid?)resultSet.ArmId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
}
