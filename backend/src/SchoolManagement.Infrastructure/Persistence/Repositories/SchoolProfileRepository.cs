using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ISchoolProfileRepository"/>.</summary>
internal sealed class SchoolProfileRepository(ApplicationDbContext context) : ISchoolProfileRepository
{
    /// <inheritdoc />
    public Task<SchoolProfile> GetTrackedSingletonAsync(CancellationToken cancellationToken) =>
        context.SchoolProfiles.FirstAsync(profile => profile.Id == SchoolProfile.SingletonId, cancellationToken);

    /// <inheritdoc />
    public Task<SchoolProfile> GetReadOnlySingletonAsync(CancellationToken cancellationToken) =>
        context.SchoolProfiles
            .AsNoTracking()
            .FirstAsync(profile => profile.Id == SchoolProfile.SingletonId, cancellationToken);
}
