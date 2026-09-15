using SchoolManagement.Domain.Admissions;

namespace SchoolManagement.Application.Abstractions.Admissions;

/// <summary>Persistence port for <see cref="AdmissionRecord"/>. One row per pupil (spec 6.5.9).</summary>
public interface IAdmissionRecordRepository
{
    /// <summary>Adds a new record. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(AdmissionRecord record, CancellationToken cancellationToken);

    /// <summary>Loads a TRACKED record by pupil id, for a command that will mutate it (<c>PATCH /admissions/{id}</c>).</summary>
    Task<AdmissionRecord?> FindTrackedByPupilIdAsync(Guid pupilId, CancellationToken cancellationToken);

    /// <summary>Loads a record by pupil id, <c>AsNoTracking</c>, for a query (<c>GET /admissions/{id}</c>).</summary>
    Task<AdmissionRecord?> FindReadOnlyByPupilIdAsync(Guid pupilId, CancellationToken cancellationToken);
}
