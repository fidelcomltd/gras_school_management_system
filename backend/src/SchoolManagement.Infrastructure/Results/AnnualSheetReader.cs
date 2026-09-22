using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Results;

/// <summary>Reads a pupil's annual result with the final arm's Third Term snapshot (only while that set is published).</summary>
internal sealed class AnnualSheetReader(ApplicationDbContext context) : IAnnualSheetReader
{
    private const int ThirdTerm = 3;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AnnualSheetData?> ReadAsync(Guid pupilId, Guid sessionId, CancellationToken cancellationToken)
    {
        var row = await context.AnnualResults.AsNoTracking()
            .FirstOrDefaultAsync(result => result.PupilId == pupilId && result.SessionId == sessionId, cancellationToken)
            .ConfigureAwait(false);
        var pupil = await context.Pupils.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == pupilId, cancellationToken).ConfigureAwait(false);
        if (row is null || pupil is null)
        {
            return null;
        }

        var snapshot = await (
                from resultSet in context.ResultSets.AsNoTracking()
                join term in context.Terms.AsNoTracking() on resultSet.TermId equals term.Id
                where resultSet.ArmId == row.ArmId && term.SessionId == sessionId && term.Ordinal == ThirdTerm && resultSet.State == ResultSetState.Published
                select resultSet.ConfigSnapshotJson)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var subjectIds = (JsonSerializer.Deserialize<List<AnnualSubjectResult>>(row.SubjectsJson, Json) ?? []).Select(subject => subject.SubjectId).ToList();
        var names = await context.Subjects.AsNoTracking()
            .Where(subject => subjectIds.Contains(subject.Id))
            .ToDictionaryAsync(subject => subject.Id, subject => subject.Name, cancellationToken)
            .ConfigureAwait(false);

        return new AnnualSheetData(
            row,
            snapshot,
            pupil.Surname,
            string.Join(' ', new[] { pupil.FirstName, pupil.MiddleName }.Where(name => !string.IsNullOrWhiteSpace(name))),
            pupil.RegistrationNumber ?? string.Empty,
            names);
    }
}
