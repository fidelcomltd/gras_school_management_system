using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IRegistrationCounterRepository"/>. Read-only by construction — see the port's own remarks.</summary>
internal sealed class RegistrationCounterRepository(ApplicationDbContext context) : IRegistrationCounterRepository
{
    /// <inheritdoc />
    public async Task<int> GetLastSerialAsync(string counterKey, CancellationToken cancellationToken)
    {
        var lastSerial = await context.RegistrationCounters
            .AsNoTracking()
            .Where(counter => counter.Id == counterKey)
            .Select(counter => (int?)counter.LastSerial)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return lastSerial ?? 0;
    }
}
