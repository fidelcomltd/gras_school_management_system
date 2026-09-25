using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Abstractions.Pupils;

/// <summary>Persistence port for <see cref="PupilStatusChange"/> (spec 6.5.14).</summary>
public interface IPupilStatusChangeRepository
{
    /// <summary>Appends a row. No <c>SaveChangesAsync</c>: the unit-of-work behaviour commits. Never updated or deleted.</summary>
    Task AddAsync(PupilStatusChange row, CancellationToken cancellationToken);

    /// <summary>Every status change for the pupil, oldest first. <c>AsNoTracking</c>.</summary>
    Task<IReadOnlyList<PupilStatusChange>> ListByPupilReadOnlyAsync(Guid pupilId, CancellationToken cancellationToken);
}
