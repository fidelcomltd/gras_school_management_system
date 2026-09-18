using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ISubjectScoreRepository"/> (TASK-0076 dispatch B).</summary>
internal sealed class SubjectScoreRepository(ApplicationDbContext context) : ISubjectScoreRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ScoreSheetRowSnapshot>> ListActiveReadOnlyAsync(
        Guid resultSetId, Guid subjectId, CancellationToken cancellationToken) =>
        await context.SubjectScores.AsNoTracking()
            .Where(score => score.ResultSetId == resultSetId && score.SubjectId == subjectId && score.VoidedAt == null)
            .Select(score => new ScoreSheetRowSnapshot(
                score.PupilId,
                score.ComponentMarksJson,
                score.ExamMark,
                score.ExamAbsent))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ResultSetMarkSnapshot>> ListAllActiveReadOnlyAsync(
        Guid resultSetId, CancellationToken cancellationToken) =>
        await context.SubjectScores.AsNoTracking()
            .Where(score => score.ResultSetId == resultSetId && score.VoidedAt == null)
            .Select(score => new ResultSetMarkSnapshot(
                score.PupilId,
                score.SubjectId,
                score.ComponentMarksJson,
                score.ExamMark,
                score.ExamAbsent))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<IReadOnlyList<SubjectScore>> ListActiveTrackedAsync(
        Guid resultSetId, Guid subjectId, CancellationToken cancellationToken) =>
        LoadTrackedAsync(resultSetId, subjectId, cancellationToken);

    private async Task<IReadOnlyList<SubjectScore>> LoadTrackedAsync(
        Guid resultSetId, Guid subjectId, CancellationToken cancellationToken) =>
        await context.SubjectScores
            .Where(score => score.ResultSetId == resultSetId && score.SubjectId == subjectId && score.VoidedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task AddAsync(SubjectScore score, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(score);
        cancellationToken.ThrowIfCancellationRequested();

        context.SubjectScores.Add(score);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(SubjectScore score, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(score);
        cancellationToken.ThrowIfCancellationRequested();

        context.SubjectScores.Remove(score);

        return Task.CompletedTask;
    }
}
