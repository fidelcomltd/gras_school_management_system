using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IResultComputationRepository"/> (TASK-0071).</summary>
internal sealed class ResultComputationRepository(ApplicationDbContext context) : IResultComputationRepository
{
    /// <inheritdoc />
    public async Task ReplaceComputedRowsAsync(
        Guid resultSetId,
        IReadOnlyList<SubjectResultLine> subjectLines,
        IReadOnlyList<SubjectArmStatistic> subjectStatistics,
        IReadOnlyList<PupilTermResult> pupilResults,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subjectLines);
        ArgumentNullException.ThrowIfNull(subjectStatistics);
        ArgumentNullException.ThrowIfNull(pupilResults);

        // Same "load existing, RemoveRange, AddRange, no SaveChangesAsync" idiom as
        // GradingBandRepository.ReplaceAllAsync — the unit-of-work behaviour commits everything
        // (this replace plus ResultSet.MarkComputed's stamp) in ONE transaction (spec 8.2 step 11).
        var existingLines = await context.SubjectResultLines
            .Where(line => line.ResultSetId == resultSetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.SubjectResultLines.RemoveRange(existingLines);
        context.SubjectResultLines.AddRange(subjectLines);

        var existingStatistics = await context.SubjectArmStatistics
            .Where(statistic => statistic.ResultSetId == resultSetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.SubjectArmStatistics.RemoveRange(existingStatistics);
        context.SubjectArmStatistics.AddRange(subjectStatistics);

        var existingResults = await context.PupilTermResults
            .Where(result => result.ResultSetId == resultSetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.PupilTermResults.RemoveRange(existingResults);
        context.PupilTermResults.AddRange(pupilResults);
    }
}
