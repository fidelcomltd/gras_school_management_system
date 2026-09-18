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

    /// <inheritdoc />
    public async Task<int> IncrementAndGetNextSerialAsync(string counterKey, CancellationToken cancellationToken)
    {
        // Raw SQL, executed immediately — never through change tracking (see the port's own remarks
        // for why: a read-modify-write through the entity would reintroduce the exact race this
        // statement exists to close). SqlQuery<T> against an INSERT ... RETURNING is the same
        // technique AdminAccountRepository.LockActiveSuperAdminIdsAsync uses for a SELECT ... FOR
        // UPDATE — Npgsql treats either as a query with a result set, not a plain non-query command.
        var rows = await context.Database
            .SqlQuery<int>(
                $"""
                INSERT INTO registration_counter (counter_key, last_serial)
                VALUES ({counterKey}, 1)
                ON CONFLICT (counter_key) DO UPDATE
                    SET last_serial = registration_counter.last_serial + 1
                RETURNING last_serial
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows[0];
    }
}
