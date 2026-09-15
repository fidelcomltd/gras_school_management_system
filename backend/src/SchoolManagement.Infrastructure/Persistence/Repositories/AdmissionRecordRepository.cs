using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Admissions;
using SchoolManagement.Domain.Admissions;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IAdmissionRecordRepository"/>.</summary>
internal sealed class AdmissionRecordRepository(ApplicationDbContext context) : IAdmissionRecordRepository
{
    /// <inheritdoc />
    public Task AddAsync(AdmissionRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        context.AdmissionRecords.Add(record);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AdmissionRecord?> FindTrackedByPupilIdAsync(Guid pupilId, CancellationToken cancellationToken) =>
        context.AdmissionRecords.FirstOrDefaultAsync(record => record.PupilId == pupilId, cancellationToken);
}
