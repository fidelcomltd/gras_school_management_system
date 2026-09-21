using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>One attendance row, read-only (TASK-0086 stage A) — for the GET sheet and for computing its current derived version before a save.</summary>
/// <param name="PupilId">The pupil this entry belongs to.</param>
/// <param name="TimesPresent">The stored value.</param>
public sealed record AttendanceEntrySnapshot(Guid PupilId, int TimesPresent);

/// <summary>Persistence port for <see cref="AttendanceEntry"/> (TASK-0086 stage A).</summary>
public interface IAttendanceEntryRepository
{
    /// <summary>Every entry for <paramref name="resultSetId"/>, read-only. <c>AsNoTracking</c>.</summary>
    Task<IReadOnlyList<AttendanceEntrySnapshot>> ListReadOnlyAsync(Guid resultSetId, CancellationToken cancellationToken);

    /// <summary>Every entry for <paramref name="resultSetId"/>, TRACKED, for a command that will update or remove some and add others.</summary>
    Task<IReadOnlyList<AttendanceEntry>> ListTrackedAsync(Guid resultSetId, CancellationToken cancellationToken);

    /// <summary>Stages a brand-new attendance row for insertion. Does NOT commit.</summary>
    Task AddAsync(AttendanceEntry entry, CancellationToken cancellationToken);

    /// <summary>Removes <paramref name="entry"/> permanently — an explicit <see langword="null"/> <c>timesPresent</c> clears a row. Does NOT commit.</summary>
    Task RemoveAsync(AttendanceEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// The highest <see cref="AttendanceEntry.TimesPresent"/> stored against ANY result set in
    /// <paramref name="termId"/> (across every arm), or <see langword="null"/> if none exists.
    /// <c>UpdateTermHandler</c> (TASK-0086 delta item 5) refuses to set
    /// <c>times_school_opened</c> below this, so the derived times-absent can never go negative.
    /// </summary>
    Task<int?> FindMaxTimesPresentByTermAsync(Guid termId, CancellationToken cancellationToken);
}
