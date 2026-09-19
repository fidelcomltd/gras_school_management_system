using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>One remark row, read-only (TASK-0086 stage A) — for the GET sheet and for computing its current derived version before a save.</summary>
/// <param name="PupilId">The pupil this remark belongs to.</param>
/// <param name="Text">The stored text.</param>
/// <param name="WrittenByName">The staff-name snapshot captured when the text last changed.</param>
/// <param name="WrittenAtUtc">When the text last changed.</param>
public sealed record PupilRemarkSnapshot(Guid PupilId, string Text, string WrittenByName, DateTimeOffset WrittenAtUtc);

/// <summary>Persistence port for <see cref="PupilRemark"/> (TASK-0086 stage A).</summary>
public interface IPupilRemarkRepository
{
    /// <summary>Every remark of <paramref name="kind"/> for <paramref name="resultSetId"/>, read-only. <c>AsNoTracking</c>.</summary>
    Task<IReadOnlyList<PupilRemarkSnapshot>> ListReadOnlyAsync(Guid resultSetId, RemarkKind kind, CancellationToken cancellationToken);

    /// <summary>Every remark of <paramref name="kind"/> for <paramref name="resultSetId"/>, TRACKED, for a command that will update or remove some and add others.</summary>
    Task<IReadOnlyList<PupilRemark>> ListTrackedAsync(Guid resultSetId, RemarkKind kind, CancellationToken cancellationToken);

    /// <summary>Stages a brand-new remark row for insertion. Does NOT commit.</summary>
    Task AddAsync(PupilRemark remark, CancellationToken cancellationToken);

    /// <summary>Removes <paramref name="remark"/> permanently — an empty or whitespace-only save clears a row. Does NOT commit.</summary>
    Task RemoveAsync(PupilRemark remark, CancellationToken cancellationToken);
}
