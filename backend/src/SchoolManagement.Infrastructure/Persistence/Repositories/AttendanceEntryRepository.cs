using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IAttendanceEntryRepository"/> (TASK-0086 stage A).</summary>
internal sealed class AttendanceEntryRepository(ApplicationDbContext context) : IAttendanceEntryRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceEntrySnapshot>> ListReadOnlyAsync(Guid resultSetId, CancellationToken cancellationToken) =>
        await context.AttendanceEntries
            .AsNoTracking()
            .Where(entry => entry.ResultSetId == resultSetId)
            .Select(entry => new AttendanceEntrySnapshot(entry.PupilId, entry.TimesPresent))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceEntry>> ListTrackedAsync(Guid resultSetId, CancellationToken cancellationToken) =>
        await context.AttendanceEntries
            .Where(entry => entry.ResultSetId == resultSetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task AddAsync(AttendanceEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        context.AttendanceEntries.Add(entry);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(AttendanceEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        context.AttendanceEntries.Remove(entry);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<int?> FindMaxTimesPresentByTermAsync(Guid termId, CancellationToken cancellationToken)
    {
        var timesPresentValues =
            from entry in context.AttendanceEntries.AsNoTracking()
            join resultSet in context.ResultSets.AsNoTracking() on entry.ResultSetId equals resultSet.Id
            where resultSet.TermId == termId
            select (int?)entry.TimesPresent;

        // MaxAsync over an empty nullable-int sequence returns null rather than throwing — exactly
        // "no attendance recorded for this term yet" (UpdateTermHandler, delta item 5).
        return await timesPresentValues.MaxAsync(cancellationToken).ConfigureAwait(false);
    }
}
