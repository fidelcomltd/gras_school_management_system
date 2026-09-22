using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Results;

/// <summary>Resolves verification tokens: the token's own revision snapshot, the set's state now, and the computed row.</summary>
internal sealed class ResultVerificationReader(ApplicationDbContext context) : IResultVerificationReader
{
    public Task<string?> FindTokenAsync(Guid resultSetId, Guid pupilId, int revisionNumber, CancellationToken cancellationToken) =>
        context.ResultVerifications.AsNoTracking()
            .Where(verification => verification.ResultSetId == resultSetId && verification.PupilId == pupilId && verification.RevisionNumber == revisionNumber)
            .Select(verification => verification.Token)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ResultVerificationRecord?> FindAsync(string token, CancellationToken cancellationToken)
    {
        var found = await (
                from verification in context.ResultVerifications.AsNoTracking()
                join resultSet in context.ResultSets.AsNoTracking() on verification.ResultSetId equals resultSet.Id
                join snapshot in context.ResultSetSnapshots.AsNoTracking()
                    on new { verification.ResultSetId, verification.RevisionNumber } equals new { snapshot.ResultSetId, snapshot.RevisionNumber }
                join pupil in context.Pupils.AsNoTracking() on verification.PupilId equals pupil.Id
                where verification.Token == token
                select new
                {
                    verification.ResultSetId,
                    verification.PupilId,
                    verification.RevisionNumber,
                    verification.IssuedAtUtc,
                    snapshot.SnapshotJson,
                    resultSet.State,
                    CurrentRevision = resultSet.RevisionNumber,
                    resultSet.PublishedAtUtc,
                    pupil.Surname,
                    pupil.FirstName,
                    pupil.MiddleName,
                    pupil.RegistrationNumber,
                })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (found is null)
        {
            return null;
        }

        var figures = await context.PupilTermResults.AsNoTracking()
            .Where(result => result.ResultSetId == found.ResultSetId && result.PupilId == found.PupilId)
            .Select(result => new { result.TotalObtained, result.TotalObtainable, result.Average, result.OverallGrade })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ResultVerificationRecord(
            found.RevisionNumber,
            found.IssuedAtUtc,
            found.SnapshotJson,
            found.State,
            found.CurrentRevision,
            found.PublishedAtUtc,
            found.Surname,
            string.Join(' ', new[] { found.FirstName, found.MiddleName }.Where(name => !string.IsNullOrWhiteSpace(name))),
            found.RegistrationNumber ?? string.Empty,
            figures?.TotalObtained,
            figures?.TotalObtainable,
            figures?.Average,
            figures?.OverallGrade);
    }
}
