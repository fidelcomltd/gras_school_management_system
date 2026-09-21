using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IPupilRemarkRepository"/> (TASK-0086 stage A).</summary>
internal sealed class PupilRemarkRepository(ApplicationDbContext context) : IPupilRemarkRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PupilRemarkSnapshot>> ListReadOnlyAsync(Guid resultSetId, RemarkKind kind, CancellationToken cancellationToken) =>
        await context.PupilRemarks
            .AsNoTracking()
            .Where(remark => remark.ResultSetId == resultSetId && remark.Kind == kind)
            .Select(remark => new PupilRemarkSnapshot(remark.PupilId, remark.Text, remark.WrittenByName, remark.WrittenAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PupilRemark>> ListTrackedAsync(Guid resultSetId, RemarkKind kind, CancellationToken cancellationToken) =>
        await context.PupilRemarks
            .Where(remark => remark.ResultSetId == resultSetId && remark.Kind == kind)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task AddAsync(PupilRemark remark, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remark);
        cancellationToken.ThrowIfCancellationRequested();

        context.PupilRemarks.Add(remark);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(PupilRemark remark, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remark);
        cancellationToken.ThrowIfCancellationRequested();

        context.PupilRemarks.Remove(remark);

        return Task.CompletedTask;
    }
}
